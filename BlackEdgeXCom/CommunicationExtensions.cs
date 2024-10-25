using NetMQ;
using NetMQ.Monitoring;
using NetMQ.Sockets;

namespace BlackEdgeCommon.Communication
{
    public static class CommunicationExtensions
    {
        private static NetMQMessage GetMessageWithIdentityHeader(NetMQFrame identity, int expectedFrameCount)
        {
            NetMQMessage message = new NetMQMessage(expectedFrameCount + 2);
            message.Append(identity);
            message.AppendEmptyFrame();
            return message;
        }

        public static bool TrySendMessageToIdentity(this RouterSocket socket, NetMQFrame identity, NetMQFrame messagePayload)
        {
            NetMQMessage message = GetMessageWithIdentityHeader(identity, 1);
            message.Append(messagePayload);
            return socket.TrySendMultipartMessage(message);
        }

        public static bool TrySendMessagesToIdentity(this RouterSocket socket, NetMQFrame identity, IEnumerable<NetMQFrame> messagePayload, int numFrames)
        {
            NetMQMessage message = GetMessageWithIdentityHeader(identity, numFrames);

            foreach (NetMQFrame frame in messagePayload)
                message.Append(frame);

            return socket.TrySendMultipartMessage(message);
        }

        public static bool TrySendMessageToIdentity(this RouterSocket socket, NetMQFrame identity, byte[] messagePayload)
        {
            NetMQMessage message = GetMessageWithIdentityHeader(identity, 1);
            message.Append(messagePayload);
            return socket.TrySendMultipartMessage(message);
        }

        public static bool TrySendMessageToIdentity(this RouterSocket socket, NetMQFrame identity, string messagePayload)
        {
            NetMQMessage message = GetMessageWithIdentityHeader(identity, 1);
            message.Append(messagePayload);
            return socket.TrySendMultipartMessage(message);
        }

        public static string GetRemoteEndpointStringSafe(this NetMQMonitorSocketEventArgs e)
        {
            string defaultValue = "unknown";
            try
            {
                return e.Socket?.RemoteEndPoint.ToString() ?? defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        public static void SetAffinityIfRequired(this SocketOptions options)
        {
            if (NetMQConfig.ThreadPoolSize != 1)
                options.Affinity = 1;
        }

        public static void SetAffinityIfRequired(this ThreadSafeSocketOptions options)
        {
            if (NetMQConfig.ThreadPoolSize != 1)
                options.Affinity = 1;
        }
    }
}
