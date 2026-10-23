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

        private const int NO_SKELETON = -1;
        private static readonly uint[] PLACEHOLDER_MATERIALS = { BridgeMaterials.PLACEHOLDER };

        private readonly List<Renderer> renderers = new (INITIAL_CAPACITY);
        private readonly List<uint> generations = new (INITIAL_CAPACITY);
        private readonly List<uint[]> materials = new (INITIAL_CAPACITY);
        private readonly Dictionary<uint, int> indexByHandle = new (INITIAL_CAPACITY);
        private readonly List<int> skeletons = new (INITIAL_CAPACITY);
        private readonly List<int> skeletonOffsets = new (16);
        private readonly List<int> skeletonCounts = new (16);

        private TransformAccessArray transforms;
        private TransformAccessArray boneTransforms;
        private NativeList<uint> handles;
        private NativeList<uint> meshes;
        private NativeList<ushort> flags;
        private NativeList<uint> materialFirst;
        private NativeList<byte> materialCount;
        private NativeList<uint> materialHandles;
        private uint nextGeneration = 1;
        private bool materialTableDirty;

        public RendererRegistry()
        {
            transforms = new TransformAccessArray(INITIAL_CAPACITY);
            boneTransforms = new TransformAccessArray(INITIAL_CAPACITY);
            handles = new NativeList<uint>(INITIAL_CAPACITY, Allocator.Persistent);
            meshes = new NativeList<uint>(INITIAL_CAPACITY, Allocator.Persistent);
            flags = new NativeList<ushort>(INITIAL_CAPACITY, Allocator.Persistent);
            materialFirst = new NativeList<uint>(INITIAL_CAPACITY, Allocator.Persistent);
            materialCount = new NativeList<byte>(INITIAL_CAPACITY, Allocator.Persistent);
            materialHandles = new NativeList<uint>(INITIAL_CAPACITY, Allocator.Persistent);
        }

        public int Count => renderers.Count;

        public TransformAccessArray Transforms => transforms;

        /// <summary>Joint transforms of every registered skeleton, in registration order.</summary>
        public TransformAccessArray BoneTransforms => boneTransforms;

        public int BoneCount => boneTransforms.isCreated ? boneTransforms.length : 0;

        public NativeList<uint> Handles => handles;

        public NativeList<uint> Meshes => meshes;

        public NativeList<ushort> Flags => flags;

        /// <summary>Per-renderer offset into <see cref="MaterialHandles" />; rebuilt lazily by <see cref="RebuildMaterialTable" />.</summary>
        public NativeList<uint> MaterialFirst => materialFirst;

        public NativeList<byte> MaterialCount => materialCount;

        public NativeList<uint> MaterialHandles => materialHandles;

        public void Dispose()
        {
            if (transforms.isCreated) transforms.Dispose();
            if (handles.IsCreated) handles.Dispose();
            if (meshes.IsCreated) meshes.Dispose();
            if (flags.IsCreated) flags.Dispose();
            if (materialFirst.IsCreated) materialFirst.Dispose();
            if (materialCount.IsCreated) materialCount.Dispose();
            if (materialHandles.IsCreated) materialHandles.Dispose();
            renderers.Clear();
            generations.Clear();
            materials.Clear();
            indexByHandle.Clear();
        }

        /// <summary>
        ///     Registers the renderer drawn as the placeholder cube with the placeholder material.
        /// </summary>
        public uint Register(Renderer renderer) =>
            Register(renderer, BridgeMeshes.PLACEHOLDER_CUBE, PLACEHOLDER_MATERIALS);

        /// <summary>
        ///     Registers the renderer with its uploaded mesh and one material handle per submesh. Registering twice
        ///     returns the existing handle.
        /// </summary>
        public uint Register(Renderer renderer, uint mesh, uint[] materialHandlesOfRenderer) =>
            Register(renderer, mesh, materialHandlesOfRenderer, NO_SKELETON);

        /// <summary>
        ///     Registers a renderer skinned to <paramref name="skeleton" /> (from <see cref="RegisterSkeleton" />).
        /// </summary>
        public uint Register(Renderer renderer, uint mesh, uint[] materialHandlesOfRenderer, int skeleton)
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
            materials.Add(materialHandlesOfRenderer);
            skeletons.Add(skeleton);
            transforms.Add(renderer.transform);
            handles.Add(handle);
            meshes.Add(mesh);
            flags.Add(FlagsOf(renderer));
            indexByHandle[handle] = renderers.Count - 1;
            materialTableDirty = true;
            return handle;
        }

        /// <summary>
        ///     Adds a skeleton (joint transforms in the order the skinned meshes index them) and returns its index;
        ///     <see cref="ClearSkeletons" /> drops them all before a rescan.
        /// </summary>
        public int RegisterSkeleton(IReadOnlyList<Transform> joints)
        {
            skeletonOffsets.Add(boneTransforms.length);
            skeletonCounts.Add(joints.Count);

            for (var i = 0; i < joints.Count; i++)
                boneTransforms.Add(joints[i]);

            return skeletonOffsets.Count - 1;
        }

        public void ClearSkeletons()
        {
            skeletonOffsets.Clear();
            skeletonCounts.Clear();

            while (boneTransforms.length > 0)
                boneTransforms.RemoveAtSwapBack(boneTransforms.length - 1);

            for (var i = 0; i < skeletons.Count; i++)
                skeletons[i] = NO_SKELETON;
        }

        public void SetSkeleton(Renderer renderer, int skeleton)
        {
            for (var i = 0; i < renderers.Count; i++)
                if (ReferenceEquals(renderers[i], renderer))
                    skeletons[i] = skeleton;
        }

        /// <summary>Bone range of the renderer at <paramref name="index" />; false when it is not skinned.</summary>
        public bool TrySkeletonOf(int index, out int boneOffset, out int boneCount)
        {
            int skeleton = skeletons[index];

            if (skeleton < 0 || skeleton >= skeletonOffsets.Count)
            {
                boneOffset = 0;
                boneCount = 0;
                return false;
            }

            boneOffset = skeletonOffsets[skeleton];
            boneCount = skeletonCounts[skeleton];
            return boneCount > 0;
        }

        public bool Contains(Renderer renderer)
        {
            for (var i = 0; i < renderers.Count; i++)
                if (ReferenceEquals(renderers[i], renderer))
                    return true;

            return false;
        }

        /// <summary>
        ///     Unregisters every renderer that was destroyed since the last call.
        /// </summary>
        public void PruneDestroyed()
        {
            for (int i = renderers.Count - 1; i >= 0; i--)
                if (renderers[i] == null)
                    Unregister(handles[i]);
        }

        /// <summary>
        ///     Rebuilds the flat material arrays when registrations changed; called before each snapshot.
        /// </summary>
        public void RebuildMaterialTable()
        {
            if (!materialTableDirty) return;

            materialTableDirty = false;
            materialFirst.Clear();
            materialCount.Clear();
            materialHandles.Clear();

            for (var i = 0; i < materials.Count; i++)
            {
                uint[] list = materials[i];
                materialFirst.Add((uint)materialHandles.Length);
                materialCount.Add((byte)Mathf.Min(list.Length, byte.MaxValue));

                for (var m = 0; m < list.Length && m < byte.MaxValue; m++)
                    materialHandles.Add(list[m]);
            }
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
                materials[index] = materials[last];
                skeletons[index] = skeletons[last];
                handles[index] = handles[last];
                meshes[index] = meshes[last];
                flags[index] = flags[last];
                indexByHandle[handles[index]] = index;
            }

            renderers.RemoveAt(last);
            generations.RemoveAt(last);
            materials.RemoveAt(last);
            skeletons.RemoveAt(last);
            materialTableDirty = true;
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
            if (renderer == null) return value;
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

    /// <summary>Mirrors `abi::MATERIAL_PLACEHOLDER`.</summary>
    public static class BridgeMaterials
    {
        public const uint PLACEHOLDER = 0;
    }
}
