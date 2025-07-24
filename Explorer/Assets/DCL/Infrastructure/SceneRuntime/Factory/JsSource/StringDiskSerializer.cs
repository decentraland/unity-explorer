using CommunityToolkit.HighPerformance;
using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.Cache.Disk;
using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Threading;
using Utility.Types;

namespace SceneRuntime.Factory.JsSource
{
    public class StringDiskSerializer : IDiskSerializer<string, SerializeMemoryIterator<StringDiskSerializer.State>>
    {
        private static readonly MemoryPool<byte> POOL = MemoryPool<byte>.Shared!;

        public readonly struct State
        {
            public readonly ReadOnlyMemory<byte> StringBytes;

            public State(ReadOnlyMemory<byte> stringBytes)
            {
                StringBytes = stringBytes;
            }
        }

        public SerializeMemoryIterator<State> Serialize(string data)
        {
            var memory = data.AsMemory();
            var bytes = memory.AsBytes();
            var state = new State(bytes);

            return SerializeMemoryIterator<State>.New(
                state,
                static (source, index, buffer) => SerializeMemoryIterator.ReadNextData(index, source.StringBytes.Span, buffer),
                static (source, index, bufferLength) => SerializeMemoryIterator.CanReadNextData(index, source.StringBytes.Length, bufferLength)
            );
        }

        public UniTask<Result<string>> DeserializeAsync(SlicedOwnedMemory<byte> data, CancellationToken token)
        {
            var charSpan = MemoryMarshal.Cast<byte, char>(data.Memory.Span);
            var output = new string(charSpan);
            data.Dispose();
            var result = Result<string>.SuccessResult(output);
            
            return UniTask.FromResult(result);
        }
    }
}
