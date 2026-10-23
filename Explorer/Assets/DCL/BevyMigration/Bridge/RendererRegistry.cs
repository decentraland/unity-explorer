using DCL.BevyMigration.Bridge.Generated;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;

namespace DCL.BevyMigration.Bridge
{
    /// <summary>
    ///     Renderers Bevy draws (BevyMigration/docs/README.md § 8 "Snapshot schema"): a generational handle per
    ///     renderer, their transforms in a <see cref="TransformAccessArray" /> for the Burst gather job, and the
    ///     parallel handle, mesh and flag arrays the snapshot points at. Everything registered is sent; Bevy culls.
    /// </summary>
    public class RendererRegistry
    {
        private const int INITIAL_CAPACITY = 256;
        private const int GENERATION_SHIFT = 20;
        private const uint INDEX_MASK = (1u << GENERATION_SHIFT) - 1;

        private readonly List<Renderer> renderers = new (INITIAL_CAPACITY);
        private readonly List<uint> generations = new (INITIAL_CAPACITY);
        private readonly Dictionary<uint, int> indexByHandle = new (INITIAL_CAPACITY);

        private TransformAccessArray transforms;
        private NativeList<uint> handles;
        private NativeList<uint> meshes;
        private NativeList<ushort> flags;
        private uint nextGeneration = 1;

        public RendererRegistry()
        {
            transforms = new TransformAccessArray(INITIAL_CAPACITY);
            handles = new NativeList<uint>(INITIAL_CAPACITY, Allocator.Persistent);
            meshes = new NativeList<uint>(INITIAL_CAPACITY, Allocator.Persistent);
            flags = new NativeList<ushort>(INITIAL_CAPACITY, Allocator.Persistent);
        }

        public int Count => renderers.Count;

        public TransformAccessArray Transforms => transforms;

        public NativeList<uint> Handles => handles;

        public NativeList<uint> Meshes => meshes;

        public NativeList<ushort> Flags => flags;

        public void Dispose()
        {
            if (transforms.isCreated) transforms.Dispose();
            if (handles.IsCreated) handles.Dispose();
            if (meshes.IsCreated) meshes.Dispose();
            if (flags.IsCreated) flags.Dispose();
            renderers.Clear();
            generations.Clear();
            indexByHandle.Clear();
        }

        /// <summary>
        ///     Registers the renderer and returns its handle. Registering twice returns the existing handle.
        /// </summary>
        public uint Register(Renderer renderer)
        {
            for (var i = 0; i < renderers.Count; i++)
                if (ReferenceEquals(renderers[i], renderer))
                    return handles[i];

            uint generation = nextGeneration++ & ((1u << (32 - GENERATION_SHIFT)) - 1);
            if (generation == 0) generation = nextGeneration++;

            var index = (uint)renderers.Count;
            uint handle = (generation << GENERATION_SHIFT) | (index & INDEX_MASK);

            renderers.Add(renderer);
            generations.Add(generation);
            transforms.Add(renderer.transform);
            handles.Add(handle);
            meshes.Add(BridgeMeshes.PLACEHOLDER_CUBE);
            flags.Add(FlagsOf(renderer));
            indexByHandle[handle] = renderers.Count - 1;
            return handle;
        }

        public void Unregister(uint handle)
        {
            if (!indexByHandle.TryGetValue(handle, out int index)) return;

            int last = renderers.Count - 1;
            indexByHandle.Remove(handle);

            if (index != last)
            {
                renderers[index] = renderers[last];
                generations[index] = generations[last];
                handles[index] = handles[last];
                meshes[index] = meshes[last];
                flags[index] = flags[last];
                indexByHandle[handles[index]] = index;
            }

            renderers.RemoveAt(last);
            generations.RemoveAt(last);
            // RemoveAtSwapBack mirrors the swap above so the transform array stays parallel.
            transforms.RemoveAtSwapBack(index);
            handles.RemoveAtSwapBack(index);
            meshes.RemoveAtSwapBack(index);
            flags.RemoveAtSwapBack(index);
        }

        /// <summary>
        ///     Refreshes per-renderer flags that change without a structural event (enabled state, shadows).
        /// </summary>
        public void RefreshFlags()
        {
            for (var i = 0; i < renderers.Count; i++)
                flags[i] = FlagsOf(renderers[i]);
        }

        /// <summary>
        ///     Hides or shows every registered renderer in Unity: Bevy owns them while the world flag says so.
        /// </summary>
        public void SetUnityRenderingEnabled(bool enabled)
        {
            for (var i = 0; i < renderers.Count; i++)
                renderers[i].forceRenderingOff = !enabled;
        }

        private static ushort FlagsOf(Renderer renderer)
        {
            var value = (ushort)0;
            if (renderer.enabled && renderer.gameObject.activeInHierarchy) value |= BridgeRendererFlags.VISIBLE;
            if (renderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) value |= BridgeRendererFlags.CAST_SHADOWS;
            if (renderer.receiveShadows) value |= BridgeRendererFlags.RECEIVE_SHADOWS;
            return value;
        }
    }

    /// <summary>Mirrors `abi::renderer_flags`.</summary>
    public static class BridgeRendererFlags
    {
        public const ushort VISIBLE = 1;
        public const ushort CAST_SHADOWS = 2;
        public const ushort RECEIVE_SHADOWS = 4;
    }

    /// <summary>Mirrors `abi::MESH_PLACEHOLDER_CUBE`.</summary>
    public static class BridgeMeshes
    {
        public const uint PLACEHOLDER_CUBE = 0;
    }
}
