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
    public class ISSDescriptorDiskSerializer : IDiskSerializer<ISSDescriptorMetadata, SerializeMemoryIterator<StringDiskSerializer.State>>
    {
        private readonly StringDiskSerializer sds = new ();

        //TODO (opti): We could serialize as blobs and not jsons
        public SerializeMemoryIterator<StringDiskSerializer.State> Serialize(ISSDescriptorMetadata data)
        {
            string json = JsonUtility.ToJson(data);
            SerializeMemoryIterator<StringDiskSerializer.State> iterator = sds.Serialize(json);
            return iterator;
        }

        public async UniTask<ISSDescriptorMetadata> DeserializeAsync(SlicedOwnedMemory<byte> data, CancellationToken token)
        {
            string json = await sds.DeserializeAsync(data, token);
            ISSDescriptorMetadata metadata = JsonUtility.FromJson<ISSDescriptorMetadata>(json);
            return metadata;
        }
    }
}
