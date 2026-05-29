using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.Cache.Disk;
using System.Text;
using System.Threading;
using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles.InitialSceneState
{
    /// <summary>
    ///     Thin JsonUtility wrapper: the on-disk shape IS <see cref="ISSDescriptorMetadata"/>, identical to
    ///     the server's descriptor JSON. The loader's resolution mode (Bundle vs Descriptor) is a live
    ///     decision (HEAD probe + manifest gate) and is intentionally not cached — a scene that resolved
    ///     to Descriptor today could legitimately resolve to Bundle tomorrow once the AB converter
    ///     publishes one. Caching only the JSON contents lets the mode be re-derived on every load.
    /// </summary>
    public class ISSDescriptorDiskSerializer : IDiskSerializer<ISSDescriptorMetadata, SerializeMemoryIterator<ISSDescriptorDiskSerializer.State>>
    {
        public SerializeMemoryIterator<State> Serialize(ISSDescriptorMetadata data)
        {
            string json = JsonUtility.ToJson(data);
            byte[] payload = Encoding.UTF8.GetBytes(json);

            var state = new State(payload);

            return SerializeMemoryIterator<State>.New(
                state,
                static (source, index, buffer) => SerializeMemoryIterator.ReadNextData(index, source.Bytes, buffer),
                static (source, index, bufferLength) => SerializeMemoryIterator.CanReadNextData(index, source.Bytes.Length, bufferLength));
        }

        public UniTask<ISSDescriptorMetadata> DeserializeAsync(SlicedOwnedMemory<byte> data, CancellationToken token)
        {
            try
            {
                string json = Encoding.UTF8.GetString(data.Memory.Span);
                return UniTask.FromResult(JsonUtility.FromJson<ISSDescriptorMetadata>(json));
            }
            finally
            {
                data.Dispose();
            }
        }

        public readonly struct State
        {
            public readonly byte[] Bytes;

            public State(byte[] bytes)
            {
                Bytes = bytes;
            }
        }
    }
}
