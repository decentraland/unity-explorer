using System;
using System.Buffers;

namespace CRDT.Protocol
{
    public static class CRDTMessageComparer
    {
        /// <summary>
        ///     The meaning of this function is to have the same reconciliation mechanism between SDK and the client
        /// </summary>
        public static int CompareData(in IMemoryOwner<byte> x, in IMemoryOwner<byte> y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            if (x.Equals(y)) return 0;

            int lengthComparison = x.Memory.Length.CompareTo(y.Memory.Length);
            if (lengthComparison != 0) return lengthComparison;

            return x.Memory.Span.SequenceCompareTo(y.Memory.Span);
        }
    }
}
