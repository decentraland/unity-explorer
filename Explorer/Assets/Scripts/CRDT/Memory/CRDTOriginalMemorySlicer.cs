using System;
using System.Buffers;
using UnityEngine.Pool;
using Utility.ThreadSafePool;

namespace CRDT.Memory
{
    /// <summary>
    /// Slices the original stream and does not use pooling
    /// </summary>
    public class CRDTOriginalMemorySlicer : ICRDTMemoryAllocator
    {
        private class SliceOwner : IMemoryOwner<byte>
        {
            private readonly CRDTOriginalMemorySlicer memorySlicer;

            internal SliceOwner(CRDTOriginalMemorySlicer memorySlicer)
            {
                this.memorySlicer = memorySlicer;
            }

            internal void Set(byte[] array)
            {
                Memory = array;
            }

            public void Dispose()
            {
                // Can't dispose the slice from the original array
                memorySlicer.memoryOwnerPool.Release(this);
            }

            public Memory<byte> Memory { get; private set; }
        }

        private static readonly ThreadSafeObjectPool<CRDTOriginalMemorySlicer> POOL = new (
            () => new CRDTOriginalMemorySlicer());

        // Introduce a pool of memory owners to prevent allocations per message
        private readonly ObjectPool<SliceOwner> memoryOwnerPool;

        private CRDTOriginalMemorySlicer()
        {
            memoryOwnerPool = new ObjectPool<SliceOwner>(
                () => new SliceOwner(this),
                defaultCapacity: 1024,
                maxSize: 1024 * 1024
            );
        }

        public static CRDTOriginalMemorySlicer Create() =>
            POOL.Get();

        public IMemoryOwner<byte> GetMemoryBuffer(in ReadOnlyMemory<byte> originalStream, int shift, int length)
        {
            var byteArray = new byte[length];
            originalStream.Span.Slice(shift, length).CopyTo(byteArray.AsSpan());
            SliceOwner sliceOwner = memoryOwnerPool.Get();
            sliceOwner.Set(byteArray);
            return sliceOwner;
        }

        public IMemoryOwner<byte> GetMemoryBuffer(int length)
        {
            SliceOwner sliceOwner = memoryOwnerPool.Get();
            sliceOwner.Set(new byte[length]);
            return sliceOwner;
        }

        public IMemoryOwner<byte> GetMemoryBuffer(in ReadOnlyMemory<byte> originalStream) =>
            GetMemoryBuffer(originalStream, 0, originalStream.Length);

        public void Dispose()
        {
            POOL.Release(this);
        }
    }
}
