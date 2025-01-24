using CommunityToolkit.HighPerformance;
using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.Cache.Disk;
using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Threading;

namespace SceneRuntime.Factory.JsSource
{
    public class StringDiskSerializer : IDiskSerializer<string>
    {
        private static readonly MemoryPool<byte> POOL = MemoryPool<byte>.Shared!;

        public UniTask<SlicedOwnedMemory<byte>> SerializeAsync(string data, CancellationToken token)
        {
            var memory = data.AsMemory();
            var bytes = memory.AsBytes();

            var buffer = POOL.Rent(bytes.Length)!;

            bytes.CopyTo(buffer.Memory);

            return new UniTask<SlicedOwnedMemory<byte>>(new SlicedOwnedMemory<byte>(buffer, bytes.Length));
        }

        public UniTask<string> DeserializeAsync(SlicedOwnedMemory<byte> data, CancellationToken token)
        {
            var charSpan = MemoryMarshal.Cast<byte, char>(data.Memory.Span);
            var output = new string(charSpan);
            data.Dispose();
            return UniTask.FromResult(output);
        }
    }
}
