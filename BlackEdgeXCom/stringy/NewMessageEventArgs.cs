using System;

namespace BlackEdgeCommon.Utils.Networking.AsyncSocket
{
    public class NewMessageEventArgs<T> : EventArgs
    {
        public T Message { get; }

        public NewMessageEventArgs(T message)
        {
            Message = message;
        }
    }
}