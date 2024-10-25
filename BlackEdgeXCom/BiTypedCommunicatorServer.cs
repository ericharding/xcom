using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMQ;

namespace BlackedgeCommon.Communication.Bidirectional.TypedCommunication
{
    public class BiTypedCommunicatorServer<TToServerMessage, TFromServerMessage> : BaseAsyncServer
    {
        public BiTypedCommunicatorServer(string ipString, int port, NetMQPoller poller = null, bool delaySocketInitialization = false)
            : base(ipString, port, poller, delaySocketInitialization)
        { }

        public BiTypedCommunicatorServer(string ipString, int port, CommunicationServicer servicer, bool delaySocketInitialization)
            : base(ipString, port, poller: servicer?.Poller, delaySocketInitialization)
        { }

        protected override void ProcessClientMessage(NetMQSocket socket, NetMQMessage netMqMessage)
        {
            // frame 0 is client identity, frame 1 is empty
            for (int i = 2; i < netMqMessage.FrameCount; i++)
            {
                TToServerMessage toServerMessage = netMqMessage[i].ToByteArray().DeserializeProtoBuf<TToServerMessage>();
                ProcessClientMessageInternal(toServerMessage);
                ProcessClientMessageWithIdentityInternal(netMqMessage.First, toServerMessage);
            }
        }

        protected virtual void ProcessClientMessageInternal(TToServerMessage message)
        { }

        protected virtual void ProcessClientMessageWithIdentityInternal(NetMQFrame identity, TToServerMessage message)
        { }

        public void SendMessageToClients(TFromServerMessage message) => SendMessageToClients(message.SerializeProtoBuf());

        protected void SendMessageToClientInternal(NetMQFrame clientIdentity, TFromServerMessage message) => SendMessageToClientInternal(clientIdentity, message.SerializeProtoBuf());
    }
}
