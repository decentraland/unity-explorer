using System;
using System.Buffers;

namespace CRDT.Memory
{
    /// <summary>
    /// Slices the original stream and does not use pooling
    /// </summary>
    public class CRDTOriginalMemorySlicer : ICRDTMemoryAllocator
    {
        private class SliceOwner : IMemoryOwner<byte>
        {
            public SliceOwner(byte[] array)
            {
                Memory = array;
            }

            public void Dispose()
            {
                // Can't dispose the slice from the original array
            }

            public Memory<byte> Memory { get; }
        }

        public IMemoryOwner<byte> GetMemoryBuffer(in ReadOnlyMemory<byte> originalStream, int shift, int length)
        {
            var byteArray = new byte[length];
            originalStream.Span.Slice(shift, length).CopyTo(byteArray.AsSpan());
            return new SliceOwner(byteArray);
        }

        public IMemoryOwner<byte> GetMemoryBuffer(int length) =>
            new SliceOwner(new byte[length]);

        public IMemoryOwner<byte> GetMemoryBuffer(in ReadOnlyMemory<byte> originalStream) =>
            GetMemoryBuffer(originalStream, 0, originalStream.Length);
    }
}
