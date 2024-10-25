using NetMQ;
using NetMQ.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlackEdgeCommon.Communication.Bidirectional
{
    public static class DealerSocketExtensions
    {
        public static bool TryGetNextMessage(this DealerSocket socket, ref Msg message, out int numFrames)
        {
            numFrames = 0;

            if (socket.TryReceive(ref message, TimeSpan.Zero) && message.HasMore)
            {
                numFrames++;
               
                while (message.HasMore)
                {
                    // blocking here to ensure we read to the end
                    socket.Receive(ref message);
                    numFrames++;
                }

                return true;
            }

            return false;
        }
    }
}
