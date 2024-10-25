using NetMQ;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlackedgeCommon.Communication.Bidirectional.TypedCommunication
{
    public class BiTypedCommunicatorClient<TToServerMessage, TFromServerMessage> : BaseAsyncClient
    {
        public BiTypedCommunicatorClient(string ipString, int port, NetMQPoller poller = null)
            : base(ipString, port, poller: poller)
        { }

        public BiTypedCommunicatorClient(string ipString, int port, bool sendHandshakeImmediately, CommunicationServicer servicer = null)
            : base(ipString, port, poller: servicer?.Poller, sendHandshakeImmediately: sendHandshakeImmediately)
        { }

        protected override void ProcessServerMessage(ReadOnlySpan<byte> message)
        {
            TFromServerMessage deserializedMessage = message.DeserializeProtoBuf<TFromServerMessage>();
            OnMessageCallback(deserializedMessage);
        }

        protected virtual void OnMessageCallback(TFromServerMessage fromServerMessage)
        { }

        public void SendMessageToServer(TToServerMessage message) => SendMessageToServer(message.SerializeProtoBuf());
    }
}
