using System;
using System.Buffers;

namespace CRDT.Memory
{
    /// <summary>
    /// Uses ArrayPool under the hood
    /// </summary>
    public class CRDTPooledMemoryAllocator : ICRDTMemoryAllocator
    {
        private class MemoryOwner : IMemoryOwner<byte>
        {
            private readonly byte[] array;

            private bool disposed;

            internal MemoryOwner(byte[] array, int size)
            {
                this.array = array;
                Memory = this.array;
                Memory = Memory.Slice(0, size);
            }

            public void Dispose()
            {
                if (!disposed)
                {
                    ArrayPool<byte>.Shared.Return(array);
                    disposed = true;
                }
            }

            public Memory<byte> Memory { get; }
        }

        public IMemoryOwner<byte> GetMemoryBuffer(in ReadOnlyMemory<byte> originalStream, int shift, int length)
        {
            byte[] byteArray = ArrayPool<byte>.Shared.Rent(length);
            originalStream.Span.Slice(shift, length).CopyTo(byteArray.AsSpan());
            return new MemoryOwner(byteArray, length);
        }

        public IMemoryOwner<byte> GetMemoryBuffer(int length)
        {
            byte[] byteArray = ArrayPool<byte>.Shared.Rent(length);
            return new MemoryOwner(byteArray, length);
        }

        //TODO (question): Can we make this class static and use this calls?
        public static IMemoryOwner<byte> Empty => new MemoryOwner(ArrayPool<byte>.Shared.Rent(0), 0);

        public static IMemoryOwner<byte> GetMemoryBuffer(in ReadOnlyMemory<byte> originalStream)
        {
            int streamLength = originalStream.Length;
            byte[] byteArray = ArrayPool<byte>.Shared.Rent(streamLength);
            originalStream.Span.Slice(0, streamLength).CopyTo(byteArray.AsSpan());
            return new MemoryOwner(byteArray, streamLength);
        }

    }
}
