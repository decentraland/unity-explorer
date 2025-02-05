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
            var meta = new Meta(data.FullFileSize);
            Span<byte> metaData = stackalloc byte[Meta.META_SIZE];
            meta.ToSpan(metaData);

            int targetSize = Meta.META_SIZE + data.FullData.Length;
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

            var partialLoadingState = new PartialLoadingState(meta.MaxFileSize);
            partialLoadingState.AppendData(fileData);
            return UniTask.FromResult(partialLoadingState);
        }

        private readonly struct Meta
        {
            public const int META_SIZE = 4;
            public readonly int MaxFileSize;

            public Meta(int maxFileSize)
            {
                this.MaxFileSize = maxFileSize;
            }

            public void ToSpan(Span<byte> span)
            {
                for (int i = 0; i < 8; i++)
                    span[i] = (byte)((MaxFileSize >> (i * 8)) & 0xFF);
            }

            public static Meta FromSpan(ReadOnlySpan<byte> array)
            {
                var maxFileSize = 0;
                for (var i = 0; i < 8; i++)
                    maxFileSize |= array[i] << (i * 8);

                return new Meta(maxFileSize);
            }
        }
    }
}
