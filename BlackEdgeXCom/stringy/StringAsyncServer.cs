using BlackEdgeCommon.Utils.Networking.AsyncSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMQ;
using System.Collections.Concurrent;
using BlackEdgeCommon.Communication.Bidirectional.MessageFormatting;

namespace BlackEdgeCommon.Communication.Bidirectional
{
    public class StringAsyncServer : BaseAsyncServer
    {
        public event EventHandler<NewMessageEventArgs<string>> NewMessageReceived;

        private readonly IMessageFormatter<string> _messageFormatter;

        public StringAsyncServer(string ipString, int port, IMessageFormatter<string> messageFormatter = null) 
            : base(ipString, port)
        {
            _messageFormatter = messageFormatter ?? new VacuousMessageFormatter<string>();
        }

        protected override void ProcessClientMessage(NetMQSocket socket, NetMQMessage netMqMessage)
        {
            if (netMqMessage.FrameCount == 3)
                NewMessageReceived?.Invoke(this, new NewMessageEventArgs<string>(netMqMessage[2].ConvertToString()));
        }

        public void SendFormattedMessageToClients(string message) => SendMessageToClients(_messageFormatter.FormatMessage(message));

        public void SendFormattedMessagesToClient(NetMQFrame clientIdentityFrame, IEnumerable<string> messages)
            => SendMessagesToClient(clientIdentityFrame, messages.Select(m => _messageFormatter.FormatMessage(m)));
    }
}
