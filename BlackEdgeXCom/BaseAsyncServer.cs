using BlackEdgeCommon.Communication.Bidirectional.MessageFormatting;
using BlackEdgeCommon.Utils.Extensions;
using BlackEdgeCommon.Utils.Logging;
using NetMQ;
using NetMQ.Monitoring;
using NetMQ.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BlackedgeCommon.Communication.Bidirectional
{
    public class BaseAsyncServer : IDisposable
    {
        public event EventHandler<NewClientIdentityConnectedEventArgs> NewClientIdentityConnected;

        private readonly Timer _watchdogTimer;

        private readonly TimeSpan _heartbeatFrequency;
        private readonly TimeSpan _watchdogFrequency;

        private RouterSocket _routerSocket;
        private NetMQTimer _heartbeatTimer;
        private NetMQPoller _poller;
        private NetMQMonitor _monitor;
        protected readonly string _serverAddress;
        private readonly int _messagesToReceiveInBatch = 500;

        private HashSet<NetMQFrame> _clientIdentities = new HashSet<NetMQFrame>();
        private HashSet<NetMQFrame> _invalidClientIdentities = new HashSet<NetMQFrame>();

        protected int NumConnectedClients => _clientIdentities.Count;

        private readonly bool _createdPoller;

        public BaseAsyncServer(string ipString, int port, NetMQPoller poller = null, bool delaySocketInitialization = false)
            : this($"tcp://{ipString}:{port}", poller, delaySocketInitialization)
        { }

        public BaseAsyncServer(string serverAddress, NetMQPoller poller = null, bool delaySocketInitialization = false)
        {
            _serverAddress = serverAddress;
            _watchdogTimer = new Timer(WatchdogTimeoutElapsed);

            _heartbeatFrequency = TimeSpan.FromSeconds(2);
            _watchdogFrequency = TimeSpan.FromSeconds(5);

            InitSocket(delaySocketInitialization);
            InitPoller(poller);

            _createdPoller = poller == null;
        }

        private void WatchdogTimeoutElapsed(object state)
            => Logger.GetLogger().Info($"[{nameof(BaseAsyncServer)}] {_serverAddress} {nameof(WatchdogTimeoutElapsed)}!");

        private void InitSocket(bool delaySocketBinding)
        {
            _routerSocket = new RouterSocket();

            _routerSocket.Options.TcpKeepalive = true;
            _routerSocket.Options.TcpKeepaliveIdle = TimeSpan.FromSeconds(5);
            _routerSocket.Options.TcpKeepaliveInterval = TimeSpan.FromSeconds(1);
            _routerSocket.Options.RouterMandatory = true; // throw if sending to client fails
            _routerSocket.Options.SetAffinityIfRequired();
            SetCustomSocketOptions(_routerSocket.Options);

            _monitor = new NetMQMonitor(_routerSocket, $"inproc://*.{_serverAddress}.monitor", SocketEvents.Accepted | SocketEvents.Disconnected);
            _monitor.Accepted += _monitor_Accepted;
            _monitor.Disconnected += _monitor_Disconnected;

            if (_poller != null)
                _monitor.AttachToPoller(_poller);

            if (!delaySocketBinding)
                BindSocket();

            _routerSocket.ReceiveReady += _routerSocket_ReceiveReady;
        }

        protected virtual void SetCustomSocketOptions(SocketOptions options)
        { }

        private void _monitor_Disconnected(object sender, NetMQMonitorSocketEventArgs e)
        {
            if (_createdPoller)
                Logger.GetLogger().Info($"[{nameof(BaseAsyncServer)}] Disconnected from client (remote endpoint = {e.GetRemoteEndpointStringSafe()}).");
            else
                Logger.GetLogger().Info($"[{nameof(BaseAsyncServer)}] {_serverAddress} Disconnected from client (remote endpoint = {e.GetRemoteEndpointStringSafe()}).");
        }

        private void _monitor_Accepted(object sender, NetMQMonitorSocketEventArgs e)
        {
            if (_createdPoller)
                Logger.GetLogger().Info($"[{nameof(BaseAsyncServer)}] Connection accepted from {e.GetRemoteEndpointStringSafe()}.");
            else
                Logger.GetLogger().Info($"[{nameof(BaseAsyncServer)}] {_serverAddress} Connection accepted from {e.GetRemoteEndpointStringSafe()}.");
        }

        protected void BindSocket()
        {
            if (_poller?.IsRunning == true)
                _poller.Run(() => _routerSocket.Bind(_serverAddress));
            else
                _routerSocket.Bind(_serverAddress);
        }

        private void InitPoller(NetMQPoller poller)
        {
            _heartbeatTimer = new NetMQTimer(_heartbeatFrequency);
            _heartbeatTimer.Elapsed += HeartbeatTimer_Elapsed;
            _heartbeatTimer.Enable = true;

            if (poller == null)
            {
                _poller = new NetMQPoller() { _routerSocket, _heartbeatTimer };
                _monitor.AttachToPoller(_poller);
                _poller.RunAsync($"{_serverAddress} Server Poller Thread");
            }
            else
            {
                _poller = poller;
                _poller.Add(_routerSocket);
                _poller.Add(_heartbeatTimer);
                _monitor.AttachToPoller(_poller);
            }

            ReScheduleWatchdogTimer();
        }

        private void HeartbeatTimer_Elapsed(object sender, NetMQTimerEventArgs e)
        {
            ReScheduleWatchdogTimer();
            SendHeartbeat();
        }

        protected void PauseWatchdogTimer() => _watchdogTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        protected void ReScheduleWatchdogTimer() => _watchdogTimer.Change(_watchdogFrequency, Timeout.InfiniteTimeSpan);

        private void SendHeartbeat() => SendMessageToClientsInternal(HandshakeHelper.HeartbeatFrame);

        protected void AddTimerToPoller(NetMQTimer timer) => _poller.Add(timer);
        protected void RemoveTimerFromPoller(NetMQTimer timer) => _poller.Remove(timer);

        private void _routerSocket_ReceiveReady(object sender, NetMQSocketEventArgs e) => MessageReadyToReceive(e.Socket);

        private void MessageReadyToReceive(NetMQSocket socket)
        {
            int messageCount = 0;
            NetMQMessage netMqMessage = new NetMQMessage();

            while (messageCount < _messagesToReceiveInBatch && socket.TryReceiveMultipartMessage(ref netMqMessage))
            {
                if (HandshakeHelper.IsHandshakeMessage(netMqMessage, HandshakeHelper.ServerFrameOffset))
                    ProcessHandshake(netMqMessage);
                else if (netMqMessage.FrameCount >= 3)
                    ProcessClientMessageHandler(socket, netMqMessage);

                messageCount++;
            }
        }

        private void ProcessClientMessageHandler(NetMQSocket socket, NetMQMessage netMqMessage)
        {
            try
            {
                ProcessClientMessage(socket, netMqMessage);
            }
            catch (Exception ex)
            {
                if (_createdPoller)
                    Logger.GetLogger().Info($"[{nameof(BaseAsyncServer)}] {nameof(ProcessClientMessageHandler)}: Exception processing client message!", ex);
                else
                    Logger.GetLogger().Info($"[{nameof(BaseAsyncServer)}] {nameof(ProcessClientMessageHandler)} {_serverAddress}: Exception processing client message!", ex);
            }
        }

        protected virtual void ProcessClientMessage(NetMQSocket socket, NetMQMessage netMqMessage)
        { }

        private void ProcessHandshake(NetMQMessage netMqMessage)
        {
            NetMQFrame clientIdentity = netMqMessage.First;
            HandshakeType handshakeType = HandshakeHelper.GetHandshakeType(netMqMessage, HandshakeHelper.ServerFrameOffset);

            switch (handshakeType)
            {
                case HandshakeType.ToggleConnection:
                    ToggleConnection(clientIdentity);
                    break;
                case HandshakeType.Logout:
                    RemoveClientIdentity(clientIdentity);
                    break;
                case HandshakeType.LoginWithSnapshot:
                    AddClientIdentity(clientIdentity, snapshotRequested: true);
                    break;
                case HandshakeType.LoginWithoutSnapshot:
                    AddClientIdentity(clientIdentity, snapshotRequested: false);
                    break;
                case HandshakeType.None:
                default:
                    break;
            }

            if (_createdPoller)
                Logger.GetLogger().Info($"Receved handshake ({handshakeType}) from {netMqMessage.First.ConvertToString()} (number of clients = {_clientIdentities.Count})");
            else
                Logger.GetLogger().Info($"Receved handshake ({handshakeType}) from {netMqMessage.First.ConvertToString()} ({_serverAddress} number of clients = {_clientIdentities.Count})");
        }

        private void AddClientIdentity(NetMQFrame clientIdentity, bool snapshotRequested)
        {
            _clientIdentities.Add(clientIdentity);
            SendMessageToClientInternalWithHandler(clientIdentity, HandshakeHelper.HandshakeFrame.AsEnumerable(), numFrames: 1, ClientErrorSingleRecipient);
            OnNewClientConnected(clientIdentity, snapshotRequested);
        }

        private void RemoveClientIdentity(NetMQFrame clientIdentity)
        {
            _clientIdentities.Remove(clientIdentity);
            OnClientDisconnected(_clientIdentities.Count);
        }

        protected virtual void OnClientDisconnected(int numRemainingClients)
        { }

        private void ToggleConnection(NetMQFrame clientIdentity)
        {
            bool existingClient = _clientIdentities.Contains(clientIdentity);
            if (existingClient)
                RemoveClientIdentity(clientIdentity);
            else
                AddClientIdentity(clientIdentity, snapshotRequested: true);
        }

        private void OnNewClientConnected(NetMQFrame newClientIdentity, bool snapshotRequested)
            => NewClientIdentityConnected?.Invoke(this, new NewClientIdentityConnectedEventArgs(newClientIdentity, snapshotRequested));

        public void SendMessageToClients(string message) => SendMessageToClients(new NetMQFrame(message));

        public void SendMessageToClients(byte[] message) => SendMessageToClients(new NetMQFrame(message));

        public void SendCommentedMessageToClients(long comment, byte[] message)
        {
            NetMQFrame commentFrame = new NetMQFrame(NetworkOrderBitsConverter.GetBytes(comment));
            NetMQFrame messageFrame = new NetMQFrame(message);
            SendMessageToClients(new List<NetMQFrame>(2) { commentFrame, messageFrame });
        }

        public void SendCommentedMessageToClients(string comment, string message)
        {
            NetMQFrame commentFrame = new NetMQFrame(comment);
            NetMQFrame messageFrame = new NetMQFrame(message);
            SendMessageToClients(new List<NetMQFrame>(2) { commentFrame, messageFrame });
        }

        protected void SendMessageToClients(IEnumerable<NetMQFrame> messageFrames) => _poller.Run(() => SendMessageToClientsInternal(messageFrames));

        private void SendMessageToClients(NetMQFrame messageFrame) => _poller.Run(() => SendMessageToClientsInternal(messageFrame));

        private void SendMessageToClientsInternal(NetMQFrame messageFrame)
        {
            foreach (NetMQFrame identity in _clientIdentities)
                SendMessageToClientInternalWithHandler(identity, messageFrame.AsEnumerable(), numFrames: 1, ClientErrorBatchOperation);

            RemoveInvalidIdentities();
        }

        protected void SendMessagesToClient(NetMQFrame clientIdentity, IEnumerable<string> messagesToSend)
            => _poller.Run(() => SendMessagesToClientInternal(clientIdentity, messagesToSend));

        private void SendMessageToClientInternalWithHandler(NetMQFrame clientIdentity, IEnumerable<NetMQFrame> messagePayload, int numFrames, Action<NetMQFrame> errorHandler)
        {
            try
            {
                if (!_routerSocket.TrySendMessagesToIdentity(clientIdentity, messagePayload, numFrames))
                    errorHandler(clientIdentity);
            }
            catch
            {
                errorHandler(clientIdentity);
            }
        }

        private void ClientErrorSingleRecipient(NetMQFrame clientIdentity)
        {
            RemoveClientIdentity(clientIdentity);

            if (_createdPoller)
                Logger.GetLogger().Info($"[{nameof(BaseAsyncServer)}]: {nameof(ClientErrorSingleRecipient)}: {clientIdentity.ConvertToString()}");
            else
                Logger.GetLogger().Info($"[{nameof(BaseAsyncServer)}]: {nameof(ClientErrorSingleRecipient)}: {_serverAddress} {clientIdentity.ConvertToString()}");
        }

        private void ClientErrorBatchOperation(NetMQFrame clientIdentity) => _invalidClientIdentities.Add(clientIdentity);

        private void RemoveInvalidIdentities()
        {
            if (_invalidClientIdentities.Count != 0)
            {
                _clientIdentities.ExceptWith(_invalidClientIdentities);
                OnClientDisconnected(_clientIdentities.Count);

                if (_createdPoller)
                    Logger.GetLogger().Info($"[{nameof(BaseAsyncServer)}]: {nameof(RemoveInvalidIdentities)}: {string.Join(",", _invalidClientIdentities.Select(f => f.ConvertToString()))}");
                else
                    Logger.GetLogger().Info($"[{nameof(BaseAsyncServer)}]: {nameof(RemoveInvalidIdentities)}: {_serverAddress} {string.Join(",", _invalidClientIdentities.Select(f => f.ConvertToString()))}");

                _invalidClientIdentities.Clear();
            }
        }

        protected void SendMessagesToClientInternal(NetMQFrame clientIdentity, IEnumerable<string> messagesToSend)
        {
            try
            {
                foreach (string message in messagesToSend)
                {
                    if (!_routerSocket.TrySendMessageToIdentity(clientIdentity, message))
                        ClientErrorSingleRecipient(clientIdentity);
                }
            }
            catch
            {
                ClientErrorSingleRecipient(clientIdentity);
            }
        }

        protected void SendMessagesToClientInternal(NetMQFrame clientIdentity, IEnumerable<byte[]> messagesToSend)
        {
            try
            {
                foreach (byte[] message in messagesToSend)
                {
                    if (!_routerSocket.TrySendMessageToIdentity(clientIdentity, message))
                        ClientErrorSingleRecipient(clientIdentity);
                }
            }
            catch
            {
                ClientErrorSingleRecipient(clientIdentity);
            }
        }

        protected void SendMessageToClientInternal(NetMQFrame clientIdentity, NetMQMessage message)
            => SendMessageToClientInternalWithHandler(clientIdentity, message, message.FrameCount, ClientErrorSingleRecipient);

        protected void SendMessageToClientInternal(NetMQFrame clientIdentity, byte[] messageToSend)
            => SendMessageToClientInternal(clientIdentity, new NetMQFrame(messageToSend));

        protected void SendMessageToClientInternal(NetMQFrame clientIdentity, string messageToSend)
            => SendMessageToClientInternal(clientIdentity, new NetMQFrame(messageToSend));

        protected void SendMessageToClientInternal(NetMQFrame clientIdentity, NetMQFrame messageToSend)
            => SendMessageToClientInternalWithHandler(clientIdentity, messageToSend.AsEnumerable(), numFrames: 1, ClientErrorSingleRecipient);

        private void SendMessageToClientsInternal(IEnumerable<NetMQFrame> messageFrames)
        {
            int numFrames = messageFrames.Count();
            foreach (NetMQFrame identity in _clientIdentities)
                SendMessageToClientInternalWithHandler(identity, messageFrames, numFrames, ClientErrorBatchOperation);

            RemoveInvalidIdentities();
        }

        protected bool CheckValidClient(NetMQFrame clientIdentity)
            => _clientIdentities.Contains(clientIdentity);

        protected void RunOnPollerThread(Action action) => _poller.Run(action);

        public virtual void Dispose()
        {
            ActionExtensions.ExecuteAndCatchException(_watchdogTimer.Dispose);
            _heartbeatTimer.Enable = false;
            _heartbeatTimer.Elapsed -= HeartbeatTimer_Elapsed;
            ActionExtensions.ExecuteAndCatchException(() => _poller.Remove(_heartbeatTimer));

            _routerSocket.ReceiveReady -= _routerSocket_ReceiveReady;
            ActionExtensions.ExecuteAndCatchException(() => _poller.RemoveAndDispose(_routerSocket));

            if (_createdPoller)
            {
                ActionExtensions.ExecuteAndCatchException(() => _poller?.Stop());
                ActionExtensions.ExecuteAndCatchException(() => _poller?.Dispose());
            }
        }

        protected void CloseConnection()
            => _poller.Run(() =>
            {
                try
                {
                    _routerSocket.Disconnect(_serverAddress);
                    Logger.GetLogger().Info($"[{nameof(BaseAsyncServer)}] {_serverAddress} {nameof(CloseConnection)}");
                }
                catch { }
            });
    }
}
