using BlackedgeCommon.Utils.Atomic;
using BlackEdgeCommon.Communication;
using BlackEdgeCommon.Communication.Bidirectional;
using NetMQ;
using NetMQ.Monitoring;
using NetMQ.Sockets;
using System;
using System.Net.Sockets;
using System.Text;

namespace BlackedgeCommon.Communication.Bidirectional
{
    public class BaseAsyncClient : IDisposable
    {
        private static readonly AtomicCountXP _monitorCounter = new AtomicCountXP();

        public event EventHandler ConnectionFailed;
        public event EventHandler ConnectionEstablished;
        public event EventHandler HeartbeatFailed;
        public event EventHandler Disconnected;

        public bool IsConnected => _connectionEstablished.IsSet();

        private DealerSocket _dealerSocket;
        private NetMQPoller _poller;
        private NetMQTimer _heartbeatTimer;
        private readonly NetMQTimer _handshakeReceiveFailureTimer;
        private NetMQMonitor _monitor;
        private string _serverAddress;

        private readonly AtomicBool _connectionEstablished;

        private bool _handshakeReceived = false;
        private readonly bool _sendMessagesBeforeHandshake;

        private volatile bool _disposing = false;
        private HandshakeType _pendingHandshakeType;

        private readonly bool _createdPoller;
        private bool _sendHandshakeOnConnect;

        public string Identity { get; private set; }

        public BaseAsyncClient(string ipString, int port, bool sendMessagesBeforeHandshake = true, NetMQPoller poller = null,
            bool sendHandshakeImmediately = true)
        {
            _serverAddress = $"tcp://{ipString}:{port}";
            _sendMessagesBeforeHandshake = sendMessagesBeforeHandshake;
            _pendingHandshakeType = HandshakeType.LoginWithSnapshot;
            _connectionEstablished = new AtomicBool(set: false);

            _handshakeReceiveFailureTimer = new NetMQTimer(TimeSpan.FromSeconds(10));
            _handshakeReceiveFailureTimer.EnableAndReset();
            _handshakeReceiveFailureTimer.Elapsed += _handshakeReceiveFailureTimer_Elapsed;

            _sendHandshakeOnConnect = sendHandshakeImmediately;

            _heartbeatTimer = new NetMQTimer(TimeSpan.FromSeconds(10));
            _heartbeatTimer.Elapsed += _hearbeatTimer_Elapsed;
            _heartbeatTimer.Enable = false;

            InitSocket(sendHandshakeImmediately);
            InitPoller(poller);

            _createdPoller = poller == null;
        }

        private void InitSocket(bool sendHandshakeImmediately)
        {
            _dealerSocket = new DealerSocket();

            _dealerSocket.Options.TcpKeepalive = true;
            _dealerSocket.Options.TcpKeepaliveIdle = TimeSpan.FromSeconds(5);
            _dealerSocket.Options.TcpKeepaliveInterval = TimeSpan.FromSeconds(1);
            _dealerSocket.Options.Linger = TimeSpan.FromSeconds(1);
            _dealerSocket.Options.ReconnectIntervalMax = TimeSpan.FromSeconds(5);
            _dealerSocket.Options.SetAffinityIfRequired();
            SetCustomSocketOptions(_dealerSocket.Options);

            string guidString = Guid.NewGuid().ToString();
            string socketIdentifier = guidString.Substring(0, Math.Min(guidString.Length, 5));
            Identity = $"{IpUtils.LocalIPAddress()}:{socketIdentifier}";
            Logger.GetLogger().Info($"[{nameof(BaseAsyncClient)}] {nameof(InitSocket)}: new {nameof(Identity)}={Identity}");
            _dealerSocket.Options.Identity = Encoding.Unicode.GetBytes(Identity);

            _monitor = new NetMQMonitor(_dealerSocket, $"inproc://*.{socketIdentifier}.{_monitorCounter.Increment()}.monitor", SocketEvents.Connected | SocketEvents.Disconnected | SocketEvents.Closed);
            _monitor.Connected += _monitor_Connected;
            _monitor.Closed += _monitor_Closed;
            _monitor.Disconnected += _monitor_Disconnected;

            if (_poller != null)
                _monitor.AttachToPoller(_poller);

            bool connectionError = false;

            try
            {
                _dealerSocket.Connect(_serverAddress);
            }
            catch (SocketException ex)
            {
                connectionError = true;
                Logger.GetLogger().Info($"[{nameof(BaseAsyncClient)}] {nameof(InitSocket)} {_serverAddress} error connecting", ex);
                ConnectionFailed?.Invoke(this, EventArgs.Empty); // Inform downstream subscribers of connection failure - may want to take more drastic action
                DelayHeartbeatTimer(); // This HeartbeatTimer _will_ expire and fire, since we couldn't connect, but this will delay our reconnect attempt by the heartbeat interval
            }

            _dealerSocket.ReceiveReady += _dealerSocket_ReceiveReady;

            if (sendHandshakeImmediately && !connectionError)
                SendHandshakeInternal();
        }

        protected virtual void SetCustomSocketOptions(SocketOptions options)
        { }

        private void _monitor_Disconnected(object sender, NetMQMonitorSocketEventArgs e)
        {
            Logger.GetLogger().Info($"[{nameof(BaseAsyncClient)}] Disconnected (remote endpoint = {_serverAddress})");
            Disconnected?.Invoke(this, e);
        }

        private void _monitor_Closed(object sender, NetMQMonitorSocketEventArgs e)
            => Logger.GetLogger().Info($"[{nameof(BaseAsyncClient)}] Connection closed (remote endpoint = {_serverAddress})");

        private void _monitor_Connected(object sender, NetMQMonitorSocketEventArgs e)
        {
            Logger.GetLogger().Info($"[{nameof(BaseAsyncClient)}] Connection established (remote endpoint = {_serverAddress})");

            // may not want to send handshake on initial connect, but do on subsequent connections
            if (_sendHandshakeOnConnect)
                SendHandshakeInternal();

            DelayHeartbeatTimer();
        }

        private void SendHandshakeInternal()
        {
            _handshakeReceiveFailureTimer.Enable = true;
            if (HandshakeHelper.TryGetHandshakeMessage(_pendingHandshakeType, out NetMQMessage handshakeMessage))
            {
                _dealerSocket.SendMultipartMessage(handshakeMessage);
                Logger.GetLogger().Info($"({Identity}) Sent handshake");
            }
        }

        private void InitPoller(NetMQPoller poller)
        {
            if (poller == null)
            {
                _poller = new NetMQPoller() { _dealerSocket, _heartbeatTimer, _handshakeReceiveFailureTimer };
                _monitor.AttachToPoller(_poller);
                _poller.RunAsync($"{_serverAddress} Client Poller Thread");
            }
            else
            {
                _poller = poller;
                _poller.Add(_dealerSocket);
                _poller.Add(_heartbeatTimer);
                _poller.Add(_handshakeReceiveFailureTimer);
                _monitor.AttachToPoller(_poller);
            }
        }

        private void _handshakeReceiveFailureTimer_Elapsed(object sender, NetMQTimerEventArgs e)
        {
            _handshakeReceiveFailureTimer.Enable = false;
            _connectionEstablished.UnSet();
            ConnectionFailed?.Invoke(this, EventArgs.Empty);
            Logger.GetLogger().Info($"{_serverAddress} Client handshake time elapsed; restarting. . .");
        }

        protected void SendHandshake()
        {
            _sendHandshakeOnConnect = true;
            if (_poller.IsRunning)
                _poller.Run(SendHandshakeInternal);
            else
                SendHandshakeInternal();
        }

        private void _hearbeatTimer_Elapsed(object sender, NetMQTimerEventArgs e)
        {
            if (!_disposing)
            {
                Logger.GetLogger().Info($"{_serverAddress} Client heartbeat time elapsed; restarting. . .");
                DisconnectDealerSocket();
                HeartbeatFailed?.Invoke(this, EventArgs.Empty);

                _heartbeatTimer.Enable = false;
                _handshakeReceived = false;
                _pendingHandshakeType = HandshakeType.LoginWithoutSnapshot;

                InitSocket(sendHandshakeImmediately: false);

                _poller.Add(_dealerSocket);
            }
        }

        private void DisconnectDealerSocket()
        {
            _dealerSocket.ReceiveReady -= _dealerSocket_ReceiveReady;
            ActionExtensions.ExecuteAndCatchException(() => _monitor?.Dispose());
            ActionExtensions.ExecuteAndCatchException(() => _poller.RemoveAndDispose(_dealerSocket));
        }

        private void _dealerSocket_ReceiveReady(object sender, NetMQSocketEventArgs e)
        {
            const int MaxMessagesToReceiveInBatch = 100;

            Msg message = new Msg();
            message.InitEmpty();
            int messagesRecieved = 0;

            while (messagesRecieved < MaxMessagesToReceiveInBatch && _dealerSocket.TryGetNextMessage(ref message, out int numFrames))
            {
                messagesRecieved++;

                if (numFrames == 2)
                {
                    if (HandshakeHelper.IsHandshakeMessage(ref message))
                        ProcessHandshake();
                    else if (!HandshakeHelper.IsHeartbeatMessage(ref message))
                        ProcessServerMessageHandler(ref message);
                }
                else if (numFrames > 2)
                    ProcessPaddedServerMessageHandler(ref message, numFrames);
            }

            message.Close();

            if (messagesRecieved != 0)
                DelayHeartbeatTimer();
        }

        private void ProcessHandshake()
        {
            _handshakeReceived = true;
            _handshakeReceiveFailureTimer.Enable = false;
            Logger.GetLogger().Info($"({Identity}) Received handshake");
            _connectionEstablished.Set();
            ConnectionEstablished?.Invoke(this, EventArgs.Empty);
        }

        private void DelayHeartbeatTimer()
        {
            if (!_disposing)
                _heartbeatTimer.EnableAndReset();
        }

        private void ExecuteIfSendReady(Action action) => _poller.Run(() =>
        {
            if (_handshakeReceived || _sendMessagesBeforeHandshake)
            {
                action();
                if (!_heartbeatTimer.Enable) // enabling also resets; don't want to reset (could already be close to elapsing)
                    _heartbeatTimer.Enable = true;
            }
        });

        public void SendMessageToServer(string message) => ExecuteIfSendReady(() => _dealerSocket.SendMoreFrameEmpty().SendFrame(message));
        public void SendMessageToServer(byte[] message) => ExecuteIfSendReady(() => _dealerSocket.SendMoreFrameEmpty().SendFrame(message));
        public void SendMessageToServer(NetMQMessage message) => ExecuteIfSendReady(() => _dealerSocket.SendMoreFrameEmpty().SendMultipartMessage(message));

        public void SendCommentedMessageToServer(string comment, byte[] message)
            => ExecuteIfSendReady(() => _dealerSocket.SendMoreFrameEmpty().SendMoreFrame(comment).SendFrame(message));

        public void SendCommentedMessageToServer(string comment, string message)
            => ExecuteIfSendReady(() => _dealerSocket.SendMoreFrameEmpty().SendMoreFrame(comment).SendFrame(message));

        public void SendCommentedMessageToServer(int comment, byte[] message)
            => ExecuteIfSendReady(() => _dealerSocket.SendMoreFrameEmpty().SendMoreFrame(NetworkOrderBitsConverter.GetBytes(comment)).SendFrame(message));

        private void ProcessServerMessageHandler(ref Msg message)
        {
            try
            {
                ProcessServerMessage(message);
                //Logger.GetLogger().Info($"{Identity} received {message.Buffer.Length} bytes.");
            }
            catch (Exception ex)
            {
                Logger.GetLogger().Info($"[{nameof(BaseAsyncClient)}] {nameof(ProcessServerMessageHandler)}: Exception processing server message!", ex);
            }
        }

        private void ProcessPaddedServerMessageHandler(ref Msg message, int frameCount)
        {
            try
            {
                ProcessPaddedServerMessage(message, frameCount);
            }
            catch (Exception ex)
            {
                Logger.GetLogger().Info($"[{nameof(BaseAsyncClient)}] {nameof(ProcessPaddedServerMessageHandler)}: Exception processing server batch message!", ex);
            }
        }

        protected virtual void ProcessServerMessage(ReadOnlySpan<byte> message)
        { }

        protected virtual void ProcessPaddedServerMessage(ReadOnlySpan<byte> message, int frameCount)
        { }

        public void Disconnect()
        {
            _dealerSocket.Disconnect(_serverAddress);
        }

        protected void RunOnPollerThread(Action action)
            => _poller.Run(action);

        public virtual void Dispose()
        {
            if (!_disposing)
            {
                _disposing = true;

                _heartbeatTimer.Enable = false;
                _heartbeatTimer.Elapsed -= _hearbeatTimer_Elapsed;
                ActionExtensions.ExecuteAndCatchException(() => _poller.Remove(_heartbeatTimer));

                _handshakeReceiveFailureTimer.Enable = false;
                _handshakeReceiveFailureTimer.Elapsed -= _handshakeReceiveFailureTimer_Elapsed;
                ActionExtensions.ExecuteAndCatchException(() => _poller.Remove(_handshakeReceiveFailureTimer));

                ActionExtensions.ExecuteAndCatchException(SendLogOff);

                DisconnectDealerSocket();

                if (_createdPoller)
                {
                    ActionExtensions.ExecuteAndCatchException(() => _poller?.Stop());
                    ActionExtensions.ExecuteAndCatchException(() => _poller?.Dispose());
                }
            }
        }

        private void SendLogOff()
        {
            _pendingHandshakeType = HandshakeType.Logout;
            SendHandshake();
        }
    }
}
