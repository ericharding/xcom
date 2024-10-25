using NetMQ;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlackEdgeCommon.Communication
{
    public static class MessageHelpers
    {
        public static int InitMessage<T>(T outgoingMessage, int maxOutgoingMessageSize, out Msg message)
        {
            message = new Msg();
            message.InitPool(maxOutgoingMessageSize);

            int messageSize = outgoingMessage.SerializeProtoBufIntoBuffer(message.SliceAsMemory());
            // Dude, why do we have a custom netmq?
            // message.SetSize(messageSize);

            return messageSize;
        }

        public static string ConvertToString(this ReadOnlySpan<byte> message)
            => Encoding.ASCII.GetString(message.ToArray());
    }
}
