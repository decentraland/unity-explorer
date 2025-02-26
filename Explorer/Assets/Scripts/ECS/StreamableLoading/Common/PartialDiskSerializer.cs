using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Buffers;
using System.Threading;

namespace ECS.StreamableLoading.Common
{
    public class PartialDiskSerializer : IDiskSerializer<PartialLoadingState>
    {
        public SlicedOwnedMemory<byte> Serialize(PartialLoadingState data) =>
            SerializeInternal(data);

        private static SlicedOwnedMemory<byte> SerializeInternal(PartialLoadingState data)
        {
            var meta = new Meta(data.FullFileSize, data.IsFileFullyDownloaded);
            Span<byte> metaData = stackalloc byte[Meta.META_SIZE];
            meta.ToSpan(metaData);

            int targetSize = Meta.META_SIZE + data.NextRangeStart;
            var memoryOwner = new SlicedOwnedMemory<byte>(MemoryPool<byte>.Shared.Rent(targetSize), targetSize);
            var memory = memoryOwner.Memory;

            metaData.CopyTo(memory.Span);
            data.FullData.Span.Slice(0, data.NextRangeStart).CopyTo(memory.Slice(Meta.META_SIZE, data.NextRangeStart).Span);

            return memoryOwner;
        }

        public UniTask<PartialLoadingState> DeserializeAsync(SlicedOwnedMemory<byte> data, CancellationToken token)
        {
            var meta = Meta.FromSpan(data.Memory.Span);
            var fileData = data.Memory.Slice(Meta.META_SIZE);

            var partialLoadingState = new PartialLoadingState(meta.MaxFileSize, meta.IsFullyDownloaded);
            partialLoadingState.AppendData(fileData);
            return UniTask.FromResult(partialLoadingState);
        }

        private readonly struct Meta
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
