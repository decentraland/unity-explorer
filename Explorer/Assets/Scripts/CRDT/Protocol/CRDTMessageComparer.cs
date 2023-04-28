using CRDT.Memory;
using System;

namespace CRDT.Protocol
{
    public static class CRDTMessageComparer
    {
        public static int CompareData(IReadOnlyMemoryOwner<byte> x, IReadOnlyMemoryOwner<byte> y) =>
            CompareData(x.ReadOnlyMemory, y.ReadOnlyMemory);

        /// <summary>
        /// The meaning of this function is to have the same reconciliation mechanism between SDK and the client
        /// </summary>
        public static int CompareData(in ReadOnlyMemory<byte> x, in ReadOnlyMemory<byte> y)
        {
            if (x.Equals(y)) return 0;

            if (x.IsEmpty && !y.IsEmpty) return -1;
            if (!x.IsEmpty && y.IsEmpty) return 1;

            // The comparison performed by Spanhelper.SequenceCompareTo is similar to a lexicographical order,
            // but it does not strictly follow the lexicographic order.
            // Instead, the comparison is done based on the binary value of each byte in the sequences.
            // This means that the ordering of the bytes is based on their numerical value
            // int result = Unsafe.AddByteOffset(ref first, offset).CompareTo(Unsafe.AddByteOffset(ref second, offset));
            // if (result != 0)
            //    return result;
            return x.Span.SequenceCompareTo(y.Span);
        }
    }
}
