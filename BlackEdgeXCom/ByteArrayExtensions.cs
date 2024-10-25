using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlackEdgeCommon.Utils.Extensions
{
    public static class ByteArrayExtensions
    {
        public static bool ByteArraySequenceEqual(byte[] byteArray1, byte[] byteArray2)
            => ByteArraySequenceEqual((ReadOnlySpan<byte>)byteArray1, (ReadOnlySpan<byte>)byteArray2);

        public static bool ByteArraySequenceEqual(ReadOnlySpan<byte> byteArray1, ReadOnlySpan<byte> byteArray2)
        {
            if (byteArray1 == null)
                return byteArray2 == null;

            return byteArray1.SequenceEqual(byteArray2);
        }
    }
}
