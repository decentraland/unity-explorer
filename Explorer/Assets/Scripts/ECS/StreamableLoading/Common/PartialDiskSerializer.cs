using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Buffers;
using System.Threading;

namespace ECS.StreamableLoading.Common
{
    public class PartialDiskSerializer : IDiskSerializer<PartialLoadingState, SerializeMemoryIterator<PartialDiskSerializer.State>>
    {
        public SerializeMemoryIterator<State> Serialize(PartialLoadingState data) =>
            SerializeInternal(data);

        private static SerializeMemoryIterator<State> SerializeInternal(PartialLoadingState data)
        {
            var meta = new Meta(data.FullFileSize, data.IsFileFullyDownloaded);
            Span<byte> metaData = stackalloc byte[Meta.META_SIZE];
            meta.ToSpan(metaData);

            var state = new State(meta, data.FullData);

            int targetSize = Meta.META_SIZE + data.NextRangeStart;
            var memoryOwner = new SlicedOwnedMemory<byte>(MemoryPool<byte>.Shared.Rent(targetSize), targetSize);
            var memory = memoryOwner.Memory;

            metaData.CopyTo(memory.Span);
            data.FullData.Span.Slice(0, data.NextRangeStart).CopyTo(memory.Slice(Meta.META_SIZE, data.NextRangeStart).Span);

            return SerializeMemoryIterator<State>.New(
                state,
                static (source, index, buffer) =>
                {
                    if (index == 0)
                    {
                        source.Meta.ToSpan(buffer.Span);
                        return Meta.META_SIZE;
                    }

                    // Address meta offset
                    index -= 1;

                    var span = source.FullData.Span;
                    return SerializeMemoryIterator.ReadNextData(index, span, buffer);

                },
                static (source, index, bufferLength) =>
                {
                    if (index == 0)
                        return true;

                    // Address meta offset
                    index -= 1;

                    return SerializeMemoryIterator.CanReadNextData(index, source.FullData.Length, bufferLength);
                }
            );

            //return memoryOwner;
        }

        public UniTask<PartialLoadingState> DeserializeAsync(SlicedOwnedMemory<byte> data, CancellationToken token)
        {
            var meta = Meta.FromSpan(data.Memory.Span);
            var fileData = data.Memory.Slice(Meta.META_SIZE);

            var partialLoadingState = new PartialLoadingState(meta.MaxFileSize, meta.IsFullyDownloaded);
            partialLoadingState.AppendData(fileData);
            return UniTask.FromResult(partialLoadingState);
        }

        public readonly struct State
        {
            public readonly Meta Meta;
            public readonly ReadOnlyMemory<byte> FullData;

            public State(Meta meta, ReadOnlyMemory<byte> fullData)
            {
                Meta = meta;
                FullData = fullData;
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
