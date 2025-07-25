using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;
using Utility.Types;

namespace ECS.StreamableLoading.Common
{
    public class PartialDiskSerializer : IDiskSerializer<PartialLoadingState, SerializeMemoryIterator<PartialDiskSerializer.State>>
    {
        public SerializeMemoryIterator<State> Serialize(PartialLoadingState data) =>
            SerializeInternal(data);

        private static SerializeMemoryIterator<State> SerializeInternal(PartialLoadingState data)
        {
            var meta = new Meta(data.FullFileSize, data.IsFileFullyDownloaded);
            var slice = data.FullData.Slice(0, data.NextRangeStart);
            var state = new State(meta, slice);

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
        }

        public UniTask<Result<PartialLoadingState>> DeserializeAsync(SlicedOwnedMemory<byte> data, CancellationToken token)
        {
            using (data)
            {
                var meta = Meta.FromSpan(data.Memory.Span);
                var fileData = data.Memory.Slice(Meta.META_SIZE);

                var partialLoadingState = new PartialLoadingState(meta.MaxFileSize, meta.IsFullyDownloaded);
                partialLoadingState.AppendData(fileData);
                
                var result = Result<PartialLoadingState>.SuccessResult(partialLoadingState);
                
                return UniTask.FromResult(result);
            }
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
