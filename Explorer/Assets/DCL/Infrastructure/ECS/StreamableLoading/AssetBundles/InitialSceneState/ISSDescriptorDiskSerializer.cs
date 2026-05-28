using Cysharp.Threading.Tasks;
using DCL.SceneRunner.Scene;
using ECS.StreamableLoading.Cache.Disk;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles.InitialSceneState
{
    /// <summary>
    ///     Serializes a resolved <see cref="ISSDescriptor"/> as a single JSON document carrying both the
    ///     resolution state (Bundle/Descriptor/None) and the descriptor metadata, so the cache files can be
    ///     opened in a text editor for debugging.
    /// </summary>
    public class ISSDescriptorDiskSerializer : IDiskSerializer<ISSDescriptor, SerializeMemoryIterator<ISSDescriptorDiskSerializer.State>>
    {
        public SerializeMemoryIterator<State> Serialize(ISSDescriptor data)
        {
            // Copy into a List<> because that's what JsonUtility serializes.
            var assets = new List<ISSDescriptorAsset>(data.Assets.Count);
            for (var i = 0; i < data.Assets.Count; i++) assets.Add(data.Assets[i]);

            string json = JsonUtility.ToJson(new DiskRecord { state = data.CurrentState, assets = assets });
            byte[] payload = Encoding.UTF8.GetBytes(json);

            var state = new State(payload);

            return SerializeMemoryIterator<State>.New(
                state,
                static (source, index, buffer) => SerializeMemoryIterator.ReadNextData(index, source.Bytes, buffer),
                static (source, index, bufferLength) => SerializeMemoryIterator.CanReadNextData(index, source.Bytes.Length, bufferLength));
        }

        public UniTask<ISSDescriptor> DeserializeAsync(SlicedOwnedMemory<byte> data, CancellationToken token)
        {
            try
            {
                string json = Encoding.UTF8.GetString(data.Memory.Span);
                DiskRecord record = JsonUtility.FromJson<DiskRecord>(json);

                return UniTask.FromResult(new ISSDescriptor(record.state, new ISSDescriptorMetadata { assets = record.assets }));
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

        /// <summary>
        ///     Wire format on disk. Distinct from <see cref="ISSDescriptorMetadata"/> (the server-side JSON
        ///     shape) because the resolution state isn't in the server payload — we compute it from a HEAD probe.
        /// </summary>
        [Serializable]
        private struct DiskRecord
        {
            public IISSDescriptor.State state;
            public List<ISSDescriptorAsset> assets;
        }
    }
}
