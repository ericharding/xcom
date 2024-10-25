using BlackEdgeCommon.Utils.Extensions;
using NetMQ;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlackEdgeCommon.Communication.Bidirectional
{
    public enum HandshakeType
    {
        None,
        ToggleConnection,
        LoginWithSnapshot,
        LoginWithoutSnapshot,
        Logout
    }

    public static class HandshakeHelper
    {
        public const int ClientFrameOffset = 0;
        public const int ServerFrameOffset = 1;

        private const int MessageTypeFrameOffset = 1;
        private const int HandshakeActionFrameOffset = 2;
        private const int SnapshotRequestFrameOffset = 3;

        private const int HeartbeatMessageFrames = 2;
        private const int HandshakeMinMessageFrames = MessageTypeFrameOffset + 1;
        private const int HandshakeWithActionMinMessageFrames = HandshakeActionFrameOffset + 1;
        private const int HandshakeWithSnapshotQualifierMinMessageFrames = SnapshotRequestFrameOffset + 1;

        private const string HandshakeMessagePayload = "HS";
        private const string LoginMessagePayload = "IN";
        private const string LogoutMessagePayload = "OUT";
        private const string SnapshotRequestPayload = "SN";

        private const string HeartbeatMessagePayload = "HB";

        public static NetMQMessage ToggleConnectionHandshakeMessage { get; }
        public static NetMQMessage LoginMessageWithSnapshot { get; }
        public static NetMQMessage LoginMessageWithoutSnapshot { get; }
        public static NetMQMessage LogoutMessage { get; }

        public static NetMQFrame HandshakeFrame { get; }
        public static NetMQFrame HeartbeatFrame { get; }

        private static byte[] _handshakeBytes;
        private static byte[] _loginBytes;
        private static byte[] _logoutBytes;
        private static byte[] _snapshotBytes;

        private static byte[] _heartbeatBytes;

        static HandshakeHelper()
        {
            ToggleConnectionHandshakeMessage = new NetMQMessage();
            ToggleConnectionHandshakeMessage.AppendEmptyFrame();
            ToggleConnectionHandshakeMessage.Append(HandshakeMessagePayload);

            LoginMessageWithSnapshot = new NetMQMessage();
            LoginMessageWithSnapshot.AppendEmptyFrame();
            LoginMessageWithSnapshot.Append(HandshakeMessagePayload);
            LoginMessageWithSnapshot.Append(LoginMessagePayload);
            LoginMessageWithSnapshot.Append(SnapshotRequestPayload);

            LoginMessageWithoutSnapshot = new NetMQMessage();
            LoginMessageWithoutSnapshot.AppendEmptyFrame();
            LoginMessageWithoutSnapshot.Append(HandshakeMessagePayload);
            LoginMessageWithoutSnapshot.Append(LoginMessagePayload);
            LoginMessageWithoutSnapshot.AppendEmptyFrame();

            LogoutMessage = new NetMQMessage();
            LogoutMessage.AppendEmptyFrame();
            LogoutMessage.Append(HandshakeMessagePayload);
            LogoutMessage.Append(LogoutMessagePayload);

            HandshakeFrame = LoginMessageWithSnapshot[MessageTypeFrameOffset];
            HeartbeatFrame = new NetMQFrame(HeartbeatMessagePayload);

            _handshakeBytes = HandshakeFrame.ToByteArray();
            _heartbeatBytes = HeartbeatFrame.ToByteArray();
            _loginBytes = LoginMessageWithSnapshot[HandshakeActionFrameOffset].ToByteArray();
            _logoutBytes = LogoutMessage[HandshakeActionFrameOffset].ToByteArray();
            _snapshotBytes = LoginMessageWithSnapshot[SnapshotRequestFrameOffset].ToByteArray();            
        }

        public static bool IsHandshakeMessage(NetMQMessage message, int frameOffset)
            => message.FrameCount >= frameOffset + HandshakeMinMessageFrames && message[frameOffset].IsEmpty 
                && ByteArrayExtensions.ByteArraySequenceEqual(message[frameOffset + MessageTypeFrameOffset].ToByteArray(), _handshakeBytes);
        
        public static bool IsHandshakeMessage(ref Msg message)
            => ByteArrayExtensions.ByteArraySequenceEqual(message, _handshakeBytes);

        public static HandshakeType GetHandshakeType(NetMQMessage message, int frameOffset)
        {
            if (message.FrameCount >= frameOffset + HandshakeWithSnapshotQualifierMinMessageFrames)
            {
                if (ByteArrayExtensions.ByteArraySequenceEqual(message[frameOffset + SnapshotRequestFrameOffset].ToByteArray(), _snapshotBytes))
                    return HandshakeType.LoginWithSnapshot;
                else
                    return HandshakeType.LoginWithoutSnapshot;
            }

            if (message.FrameCount >= frameOffset + HandshakeWithActionMinMessageFrames)
            {
                if (ByteArrayExtensions.ByteArraySequenceEqual(message[frameOffset + HandshakeActionFrameOffset].ToByteArray(), _logoutBytes))
                    return HandshakeType.Logout;
                else
                    return HandshakeType.LoginWithSnapshot; // assuming an "un-qualified" snapshot state is snapshot request
            }

            if (message.FrameCount >= frameOffset + HandshakeMinMessageFrames)
                return HandshakeType.ToggleConnection; // assuming we haven't called this with the wrong message type

            return HandshakeType.None;
        }

        public static bool TryGetHandshakeMessage(HandshakeType handshakeType, out NetMQMessage handshakeMessage)
        {
            switch (handshakeType)
            {
                case HandshakeType.LoginWithSnapshot:
                    handshakeMessage = LoginMessageWithSnapshot;
                    return true;
                case HandshakeType.LoginWithoutSnapshot:
                    handshakeMessage = LoginMessageWithoutSnapshot;
                    return true;
                case HandshakeType.Logout:
                    handshakeMessage = LogoutMessage;
                    return true;
                case HandshakeType.ToggleConnection:
                    handshakeMessage = ToggleConnectionHandshakeMessage;
                    return true;

                case HandshakeType.None:
                default:
                    handshakeMessage = null;
                    return false;
            }
        }

        public static bool IsHeartbeatMessage(NetMQMessage message, int frameOffset)
            => message.FrameCount == frameOffset + HeartbeatMessageFrames && message[frameOffset].IsEmpty
                && ByteArrayExtensions.ByteArraySequenceEqual(message[frameOffset + MessageTypeFrameOffset].ToByteArray(), _heartbeatBytes);

        public static bool IsHeartbeatMessage(ref Msg message)
            => ByteArrayExtensions.ByteArraySequenceEqual(message, _heartbeatBytes);
    }
}
