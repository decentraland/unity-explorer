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

            if (x.Memory.IsEmpty && !y.Memory.IsEmpty) return -1;
            if (!x.Memory.IsEmpty && y.Memory.IsEmpty) return 1;

            // The comparison performed by Spanhelper.SequenceCompareTo is similar to a lexicographical order,
            // but it does not strictly follow the lexicographic order.
            // Instead, the comparison is done based on the binary value of each byte in the sequences.
            // This means that the ordering of the bytes is based on their numerical value
            // int result = Unsafe.AddByteOffset(ref first, offset).CompareTo(Unsafe.AddByteOffset(ref second, offset));
            // if (result != 0)
            //    return result;
            return x.Memory.Span.SequenceCompareTo(y.Memory.Span);
        }
    }
}
