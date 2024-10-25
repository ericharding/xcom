using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlackEdgeCommon.Communication.Bidirectional.MessageFormatting
{
    class VacuousMessageFormatter<TMessage> : IMessageFormatter<TMessage>
    {
        public TMessage FormatMessage(TMessage unformattedMessage)
        {
            return unformattedMessage;
        }
    }
}
