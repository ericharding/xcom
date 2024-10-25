using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlackEdgeCommon.Communication.Bidirectional.MessageFormatting
{
    public interface IMessageFormatter<TMessage>
    {
        TMessage FormatMessage(TMessage unformattedMessage);
    }
}
