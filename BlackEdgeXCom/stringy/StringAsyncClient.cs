using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMQ;
using BlackEdgeCommon.Communication.Bidirectional.MessageFormatting;
using BlackEdgeCommon.Communication.Bidirectional;
using BlackEdgeCommon.Utils.Networking.AsyncSocket;

namespace BlackEdgeCommon.Communication.Bidirectional
{
    public class StringAsyncClient : BaseAsyncClient
    {
        public event EventHandler<NewMessageEventArgs<string>> NewMessageReceived;

        private readonly IMessageFormatter<string> _messageFormatter;

        public StringAsyncClient(string ipString, int port, IMessageFormatter<string> messageFormatter = null, bool sendMessagesBeforeHandshake = true,
            CommunicationServicer servicer = null, bool sendHandshakeImmediately = true)
            : base(ipString, port, sendMessagesBeforeHandshake, servicer?.Poller, sendHandshakeImmediately)
        {
            _messageFormatter = messageFormatter ?? new VacuousMessageFormatter<string>();
        }

        protected override void ProcessServerMessage(ReadOnlySpan<byte> message)
        {
            string deserialized = message.ConvertToString();
            NewMessageReceived?.Invoke(this, new NewMessageEventArgs<string>(deserialized));
        }

        public void SendFormattedMessageToServer(string message) => SendMessageToServer(_messageFormatter.FormatMessage(message));
    }
}
