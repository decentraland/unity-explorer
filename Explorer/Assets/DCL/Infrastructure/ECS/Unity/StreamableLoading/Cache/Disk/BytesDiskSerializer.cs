using Cysharp.Threading.Tasks;
using System.Threading;

namespace ECS.StreamableLoading.Cache.Disk
{
    /// <summary>
    ///     Stores a byte array as is, for files the consumer decodes itself.
    /// </summary>
    public class BytesDiskSerializer : IDiskSerializer<byte[], SerializeMemoryIterator<BytesDiskSerializer.State>>
    {
        public readonly struct State
        {
            public readonly byte[] Bytes;

            public State(byte[] bytes)
            {
                Bytes = bytes;
            }
        }

        public SerializeMemoryIterator<State> Serialize(byte[] data) =>
            SerializeMemoryIterator<State>.New(
                new State(data),
                static (source, index, buffer) => SerializeMemoryIterator.ReadNextData(index, source.Bytes, buffer),
                static (source, index, bufferLength) => SerializeMemoryIterator.CanReadNextData(index, source.Bytes.Length, bufferLength)
            );

        public UniTask<byte[]> DeserializeAsync(SlicedOwnedMemory<byte> data, CancellationToken token)
        {
            byte[] bytes = data.Memory.Span.ToArray();
            data.Dispose();
            return UniTask.FromResult(bytes);
        }
    }
}
