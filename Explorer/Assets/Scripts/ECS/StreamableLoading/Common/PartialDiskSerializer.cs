using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Memory;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace ECS.StreamableLoading.Common
{
    public class PartialDiskSerializer : IDiskSerializer<PartialLoadingState, PartialDiskSerializer.PartialMemoryIterator>
    {
        public PartialMemoryIterator Serialize(PartialLoadingState data) =>
            SerializeInternal(data);

        private static PartialMemoryIterator SerializeInternal(PartialLoadingState data)
        {
            var memory = data.PeekMemory();
            var meta = new Meta(data.FullFileSize, memory.TotalLength, data.IsFileFullyDownloaded);
            return new PartialMemoryIterator(meta, memory, false);
        }

        public UniTask<PartialLoadingState> DeserializeAsync(SlicedOwnedMemory<byte> data, CancellationToken token)
        {
            using (data)
            {
                var meta = Meta.FromSpan(data.Memory.Span);
                var fileData = data.Memory.Slice(Meta.META_SIZE).Span;

                if (meta.WrittenBytesSize != fileData.Length)
                {
                    ReportHub.LogError(ReportCategory.DISK_CACHE, $"Actual length {fileData.Length} not equals to declared length {meta.WrittenBytesSize}");
                    return UniTask.FromResult(new PartialLoadingState(meta.MaxFileSize));
                }

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
            private readonly bool ownChain;
            private ChainMemoryIterator iterator;
            private int index;

            public PartialMemoryIterator(Meta meta, MemoryChain ownedChain, bool ownChain) : this()
            {
                unsafe
                {
                    ptr = NativeAlloc.Malloc((nuint)Meta.META_SIZE);
                    metaMemory = UnmanagedMemoryManager<byte>.New(ptr.ToPointer(), Meta.META_SIZE);
                    var span = metaMemory.Memory.Span;
                    meta.ToSpan(span);
                }

                this.ownedChain = ownedChain;
                this.ownChain = ownChain;
                iterator = ownedChain.AsMemoryIterator();
                index = -1;
            }

            public void Dispose()
            {
                NativeAlloc.Free(ptr);

                UnmanagedMemoryManager<byte>.Release(metaMemory);
                iterator.Dispose();

                if (ownChain)
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
            public static int META_SIZE
            {
                get
                {
                    unsafe { return sizeof(Meta); }
                }
            }

            public readonly int MaxFileSize;
            public readonly int WrittenBytesSize;
            public readonly bool IsFullyDownloaded;

            public Meta(int maxFileSize, int writtenBytesSize, bool isFullyDownloaded)
            {
                this.MaxFileSize = maxFileSize;
                this.WrittenBytesSize = writtenBytesSize;
                this.IsFullyDownloaded = isFullyDownloaded;
            }

            public void ToSpan(Span<byte> span)
            {
                var self = this;
                var origin = MemoryMarshal.CreateReadOnlySpan(ref self, 1);
                var raw = MemoryMarshal.AsBytes(origin);

                raw.CopyTo(span);
            }

            public static Meta FromSpan(ReadOnlySpan<byte> array) =>
                MemoryMarshal.Read<Meta>(array);
        }
    }
}
