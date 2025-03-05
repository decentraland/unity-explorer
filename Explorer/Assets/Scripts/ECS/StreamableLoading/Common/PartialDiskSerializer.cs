using Cysharp.Threading.Tasks;
using DCL.Optimization.Memory;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace ECS.StreamableLoading.Common
{
    public class PartialDiskSerializer : IDiskSerializer<PartialLoadingState, PartialDiskSerializer.PartialMemoryIterator>
    {
        public PartialMemoryIterator Serialize(PartialLoadingState data) =>
            SerializeInternal(data);

        private static PartialMemoryIterator SerializeInternal(PartialLoadingState data)
        {
            var meta = new Meta(data.FullFileSize, data.IsFileFullyDownloaded);
            var copy = data.DeepCopy();
            var transferred = copy.TransferMemoryOwnership();
            return new PartialMemoryIterator(meta, transferred);
        }

        public UniTask<PartialLoadingState> DeserializeAsync(SlicedOwnedMemory<byte> data, CancellationToken token)
        {
            using (data)
            {
                var meta = Meta.FromSpan(data.Memory.Span);
                var fileData = data.Memory.Slice(Meta.META_SIZE).Span;

                var partialLoadingState = new PartialLoadingState(meta.MaxFileSize, meta.IsFullyDownloaded);
                partialLoadingState.AppendData(fileData);
                return UniTask.FromResult(partialLoadingState);
            }
        }

        public struct PartialMemoryIterator : IMemoryIterator
        {
            private readonly IntPtr ptr;
            private readonly UnmanagedMemoryManager<byte> metaMemory;
            private readonly MemoryChain ownedChain;
            private ChainMemoryIterator iterator;
            private int index;

            public PartialMemoryIterator(Meta meta, MemoryChain ownedChain) : this()
            {
                unsafe
                {
                    void* p = UnsafeUtility.Malloc(Meta.META_SIZE, 16, Allocator.Persistent)!;
                    ptr = new IntPtr(p);
                    metaMemory = UnmanagedMemoryManager<byte>.New(p, Meta.META_SIZE);
                    var span = metaMemory.Memory.Span;
                    meta.ToSpan(span);
                }

                this.ownedChain = ownedChain;
                iterator = ownedChain.AsMemoryIterator();
                index = -1;
            }

            public void Dispose()
            {
                unsafe { UnsafeUtility.Free(ptr.ToPointer()!, Allocator.Persistent); }

                UnmanagedMemoryManager<byte>.Release(metaMemory);
                iterator.Dispose();
                ownedChain.Dispose();
            }

            public readonly ReadOnlyMemory<byte> Current
            {
                get
                {
                    if (index == -1)
                        throw new InvalidOperationException("Current is not valid before MoveNext is called");

                    if (index == 0)
                        return metaMemory.Memory;

                    if (index == 1)
                        return iterator.Current;

                    throw new InvalidOperationException("Current is not valid after MoveNext returns false");
                }
            }

            public readonly int? TotalSize => Meta.META_SIZE + iterator.TotalSize;

            public bool MoveNext()
            {
                switch (index)
                {
                    case -1:
                        index = 0;
                        return true;
                    case 0:
                        index = 1;
                        return iterator.MoveNext();
                    case 1:
                        return iterator.MoveNext();
                }

                return false;
            }
        }

        public readonly struct Meta
        {
            public const int META_SIZE = 5;
            public readonly int MaxFileSize;
            public readonly bool IsFullyDownloaded;

            public Meta(int maxFileSize, bool isFullyDownloaded)
            {
                this.MaxFileSize = maxFileSize;
                this.IsFullyDownloaded = isFullyDownloaded;
            }

            public void ToSpan(Span<byte> span)
            {
                span[0] = (byte)(IsFullyDownloaded ? 1 : 0);

                for (int i = 1; i < 5; i++)
                    span[i] = (byte)((MaxFileSize >> (i * 8)) & 0xFF);
            }

            public static Meta FromSpan(ReadOnlySpan<byte> array)
            {
                var maxFileSize = 0;
                var isFullyDownloaded = array[0] == 1;

                for (var i = 1; i < 5; i++)
                    maxFileSize |= array[i] << (i * 8);

                return new Meta(maxFileSize, isFullyDownloaded);
            }
        }
    }
}
