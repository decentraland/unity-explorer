using Cysharp.Threading.Tasks;
using DCL.SceneRunner.Scene;
using ECS.StreamableLoading.Cache.Disk;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles.InitialSceneState
{
    /// <summary>
    ///     Persists only the descriptor's asset list — the resolution mode (Bundle vs Descriptor) is a live
    ///     decision (HEAD probe + manifest gate) and must not be cached: a scene that resolved to Descriptor
    ///     today could legitimately resolve to Bundle tomorrow once the AB converter publishes one, and a
    ///     cached State field would freeze the wrong answer forever. So we cache the *expensive* part (the
    ///     JSON contents) and re-derive the mode on every load. On cache hit we hand back Descriptor mode —
    ///     that's the only mode the loader currently emits; when Bundle mode is re-enabled, the loader will
    ///     short-circuit the JSON fetch on a hit and still run the HEAD probe before returning.
    ///     <para>
    ///     The on-disk shape is intentionally identical to the server's descriptor JSON
    ///     (<see cref="ISSDescriptorMetadata"/>), so cache files are interchangeable with raw server payloads
    ///     and open in a text editor for debugging.
    ///     </para>
    /// </summary>
    public class ISSDescriptorDiskSerializer : IDiskSerializer<ISSDescriptorResolution, SerializeMemoryIterator<ISSDescriptorDiskSerializer.State>>
    {
        public SerializeMemoryIterator<State> Serialize(ISSDescriptorResolution data)
        {
            // Copy into a List<> because that's what JsonUtility serializes.
            IReadOnlyList<ISSDescriptorAsset>? source = data.Assets;
            var assets = new List<ISSDescriptorAsset>(source?.Count ?? 0);
            if (source != null)
                for (var i = 0; i < source.Count; i++) assets.Add(source[i]);

            string json = JsonUtility.ToJson(new ISSDescriptorMetadata { assets = assets });
            byte[] payload = Encoding.UTF8.GetBytes(json);

            var state = new State(payload);

            return SerializeMemoryIterator<State>.New(
                state,
                static (source, index, buffer) => SerializeMemoryIterator.ReadNextData(index, source.Bytes, buffer),
                static (source, index, bufferLength) => SerializeMemoryIterator.CanReadNextData(index, source.Bytes.Length, bufferLength));
        }

        public UniTask<ISSDescriptorResolution> DeserializeAsync(SlicedOwnedMemory<byte> data, CancellationToken token)
        {
            try
            {
                string json = Encoding.UTF8.GetString(data.Memory.Span);
                ISSDescriptorMetadata metadata = JsonUtility.FromJson<ISSDescriptorMetadata>(json);

                // Cache hit means "this scene's descriptor JSON existed at some point" — Descriptor is the
                // only mode the loader currently emits. When Bundle mode is re-enabled, replace this with a
                // HEAD probe (and bump GetISSDescriptor.DiskHashCompute.ITERATION_NUMBER to be safe).
                return UniTask.FromResult(new ISSDescriptorResolution(IISSDescriptor.State.Descriptor, metadata.assets));
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
