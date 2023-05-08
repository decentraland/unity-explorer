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
            public SliceOwner(ReadOnlyMemory<byte> memory)
            {
                ReadOnlyMemory = memory;
            }

            public void Dispose()
            {
                // Can't dispose the slice from the original array
            }

            public ReadOnlyMemory<byte> ReadOnlyMemory { get; }

            public Memory<byte> Memory { get; }
        }

        public IMemoryOwner<byte> GetMemoryBuffer(in ReadOnlyMemory<byte> originalStream, int shift, int length)
        {
            var slice = originalStream.Slice(shift, length);
            return new SliceOwner(slice);
        }

        public IMemoryOwner<byte> GetMemoryBuffer(int length)
        {
            byte[] byteArray = ArrayPool<byte>.Shared.Rent(length);
            return new SliceOwner(byteArray);
        }

        public IMemoryOwner<byte> GetMemoryBuffer(in ReadOnlyMemory<byte> originalStream)
        {
            byte[] byteArray = ArrayPool<byte>.Shared.Rent(originalStream.Length);
            originalStream.Span.Slice(0, originalStream.Length).CopyTo(byteArray.AsSpan());
            return new SliceOwner(byteArray);
        }
    }
}
