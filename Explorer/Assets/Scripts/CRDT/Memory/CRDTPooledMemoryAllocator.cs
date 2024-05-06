using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using DCL.Utilities.Extensions;
using System;
using System.Buffers;
using UnityEngine;
using UnityEngine.Pool;

namespace CRDT.Memory
{
    /// <summary>
    ///     Allocates chunks for CRDT Messages from the Array Pool unique for each scene (thread)
    /// </summary>
    public class CRDTPooledMemoryAllocator : ICRDTMemoryAllocator
    {
        private static readonly ThreadSafeObjectPool<CRDTPooledMemoryAllocator> POOL = new (
            () => new CRDTPooledMemoryAllocator(), defaultCapacity: PoolConstants.SCENES_COUNT);

        // Introduce a pool of memory owners to prevent allocations per message
        private readonly IObjectPool<MemoryOwner> memoryOwnerPool;

        private readonly ArrayPool<byte> arrayPool;

        private CRDTPooledMemoryAllocator()
        {
            // <summary>The default maximum length of each array in the pool (2^20).</summary>
            // private const int DefaultMaxArrayLength = 1024 * 1024;
            // <summary>The default maximum number of arrays per bucket that are available for rent.</summary>
            // private const int DefaultMaxNumberOfArraysPerBucket = 50;

            // 50 will not work for us as we have much more than 50 similar sized components (e.g. for every SDKTransform)
            // cap at 1MB as it's highly unlikely that components will be bigger than that

            // 1024 similar sized components (probably the same components)
            // TODO add analytics that will signal if our assumptions are wrong
            arrayPool = ArrayPool<byte>.Create(1024 * 1024, 1024)!;

            memoryOwnerPool = new ThreadSafeObjectPool<MemoryOwner>(
                () => new MemoryOwner(this),
                defaultCapacity: 1024,
                maxSize: 1024 * 1024,
                collectionCheck: false // hot path
            );
        }

        public void Dispose()
        {
            POOL.Release(this);
        }

        public static CRDTPooledMemoryAllocator Create() =>
            POOL.Get()!;

        public IMemoryOwner<byte> GetMemoryBuffer(in ReadOnlyMemory<byte> originalStream, int shift, int length)
        {
            try
            {
                if (length > originalStream.Length)
                    throw new Exception($"Length is too big: {length}, originalStreamSize: {originalStream.Length}");

                byte[] byteArray = arrayPool.Rent(length)!;
                originalStream.Span.Slice(shift, length).CopyTo(byteArray.AsSpan());
                MemoryOwner memoryOwner = memoryOwnerPool.Get()!;
                memoryOwner.Set(byteArray, length);
                return memoryOwner;
            }
            catch (Exception e) { throw new Exception($"Cannot provide MemoryBuffer originalStreamSize: {originalStream.Length} with shift: {shift} with length: {length}", e); }
        }

        public IMemoryOwner<byte> GetMemoryBuffer(int length)
        {
            MemoryOwner memoryOwner = memoryOwnerPool.Get()!;
            memoryOwner.Set(arrayPool.Rent(length)!, length);
            return memoryOwner;
        }

        private class MemoryOwner : IMemoryOwner<byte>
        {
            private readonly CRDTPooledMemoryAllocator crdtPooledMemoryAllocator;
            private byte[]? array;

            private bool disposed;

            private Memory<byte> slicedMemory;

            public Memory<byte> Memory => disposed ? throw new ObjectDisposedException(nameof(CRDTPooledMemoryAllocator) + "." + nameof(MemoryOwner)) : slicedMemory;

            internal MemoryOwner(CRDTPooledMemoryAllocator crdtPooledMemoryAllocator)
            {
                this.crdtPooledMemoryAllocator = crdtPooledMemoryAllocator;
            }

            public void Dispose()
            {
                if (disposed)
                    return;

                // it is mandatory to have two-level pool as the size of the array
                // on every rent can be absolutely different
                crdtPooledMemoryAllocator.arrayPool.Return(array.EnsureNotNull("MemoryOwner array has not been set"));
                crdtPooledMemoryAllocator.memoryOwnerPool.Release(this);
                array = null;
                slicedMemory = Memory<byte>.Empty;
                disposed = true;
            }

            internal void Set(byte[] array, int size)
            {
                this.array = array;
                slicedMemory = this.array.AsMemory().Slice(0, size);
                disposed = false;
            }
        }
    }
}
