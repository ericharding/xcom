using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BlackEdgeCommon.Utils.Atomic
{
    public class AtomicBool
    {
        private const int TrueValue = 1;
        private const int FalseValue = 0;

        private int _value;

        public AtomicBool(bool set) => _value = set ? TrueValue : FalseValue;

        public bool IsSet() => Interlocked.CompareExchange(ref _value, TrueValue, TrueValue) == TrueValue;
        public void Set() => Interlocked.Exchange(ref _value, TrueValue);
        public void UnSet() => Interlocked.Exchange(ref _value, FalseValue);
        public void SetTo(bool value) => Interlocked.Exchange(ref _value, value ? TrueValue : FalseValue);

        public bool CheckSetAndUnset() => Interlocked.CompareExchange(ref _value, FalseValue, TrueValue) == TrueValue;
        public bool CheckUnsetAndSet() => Interlocked.CompareExchange(ref _value, TrueValue, FalseValue) == FalseValue;
    }
}
