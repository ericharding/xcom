using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlackEdgeCommon.Utils.Database
{
    public interface ITimeStampedRecord
    {
        DateTime RecordTime { get; set; }
    }
}
