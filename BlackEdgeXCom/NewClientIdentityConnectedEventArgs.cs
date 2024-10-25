using NetMQ;
using System;

namespace BlackEdgeCommon.Communication.Bidirectional
{
    public class NewClientIdentityConnectedEventArgs : EventArgs
    {
        public NetMQFrame ClientIdentity { get; }
        public string ClientIdentityString { get; }
        public bool SnapshotRequested { get; }

        public NewClientIdentityConnectedEventArgs(NetMQFrame clientIdentity)
            : this(clientIdentity, snapshotRequested: true)
        { }

        public NewClientIdentityConnectedEventArgs(NetMQFrame clientIdentity, bool snapshotRequested)
        {
            ClientIdentity = clientIdentity;
            ClientIdentityString = clientIdentity.ConvertToString();
            SnapshotRequested = snapshotRequested;
        }
    }
}