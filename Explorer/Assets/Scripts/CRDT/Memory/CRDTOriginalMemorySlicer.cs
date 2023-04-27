using System;

namespace CRDT.Memory
{
    /// <summary>
    /// Slices the original stream and does not use pooling
    /// </summary>
    public class CRDTOriginalMemorySlicer : ICRDTMemoryAllocator
    {
        private class SliceOwner : IReadOnlyMemoryOwner<byte>
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
        }

        public IReadOnlyMemoryOwner<byte> GetMemoryBuffer(in ReadOnlyMemory<byte> originalStream, int shift, int length)
        {
            var slice = originalStream.Slice(shift, length);
            return new SliceOwner(slice);
        }
    }
}
