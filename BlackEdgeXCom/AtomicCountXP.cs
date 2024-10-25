using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BlackedgeCommon.Utils.Atomic
{
    public class AtomicCountXP
    {
        public int Count => Interlocked.CompareExchange(ref _count, 0, 0);
        private int _count;

        public AtomicCountXP(int initialCount)
        {
            _count = initialCount;
        }

        public AtomicCountXP()
            : this(0)
        { }

        public int Increment() => Interlocked.Increment(ref _count);
        public int Decrement() => Interlocked.Decrement(ref _count);
        public int Add(int valueToAdd) => Interlocked.Add(ref _count, valueToAdd);
        public int Subtract(int count) => Add(-1 * count);
        public int Reset() => Interlocked.Exchange(ref _count, 0);
        public void SetTo(int count) => Interlocked.Exchange(ref _count, count);

        public override string ToString() => $"{Count}";
    }
}
