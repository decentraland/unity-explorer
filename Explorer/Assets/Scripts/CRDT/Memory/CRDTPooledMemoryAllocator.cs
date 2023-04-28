using System;
using System.Buffers;

namespace CRDT.Memory
{
    /// <summary>
    /// Uses ArrayPool under the hood
    /// </summary>
    public class CRDTPooledMemoryAllocator : ICRDTMemoryAllocator
    {
        private class MemoryOwner : IReadOnlyMemoryOwner<byte>
        {
            private readonly byte[] array;

            private bool disposed;

            internal MemoryOwner(byte[] array, int size)
            {
                this.array = array;
                ReadOnlyMemory = this.array;
                ReadOnlyMemory = ReadOnlyMemory.Slice(size);
            }

            public void Dispose()
            {
                if (!disposed)
                {
                    ArrayPool<byte>.Shared.Return(array);
                    disposed = true;
                }
            }

            public ReadOnlyMemory<byte> ReadOnlyMemory { get; }
        }

        public IReadOnlyMemoryOwner<byte> GetMemoryBuffer(in ReadOnlyMemory<byte> originalStream, int shift, int length)
        {
            var memoryOwner = MemoryPool<byte>.Shared.Rent(length);
            var slice = originalStream.Slice(shift, length);
            slice.CopyTo(memoryOwner.Memory);

            return new MemoryOwner(ArrayPool<byte>.Shared.Rent(length), length);
        }
    }
}
