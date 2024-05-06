using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.Optimization.Pools;
using System.Collections.Generic;
using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.Diagnostics;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    /// <summary>
    ///     Stores data for the compute shader to perform skinning
    /// </summary>
    public struct AvatarCustomSkinningComponent
    {
        public struct Buffers
        {
            internal ComputeSkinningBufferContainer computeSkinningBufferContainer;
            internal readonly ComputeBuffer bones;
            internal readonly int kernel;

            public Buffers(ComputeBuffer bones, int kernel) : this()
            {
                this.bones = bones;
                this.kernel = kernel;
            }

            public void DisposeBuffers()
            {
                computeSkinningBufferContainer.Dispose();
                bones.Dispose();
            }
        }

        public struct MaterialSetup
        {
            internal readonly TextureArraySlot?[] usedTextureArraySlots;
            /// <summary>
            ///     Cel Shading Material is created based on the original material
            /// </summary>
            internal readonly Material usedMaterial;

            internal readonly int shaderId;

            public MaterialSetup(TextureArraySlot?[] usedTextureArraySlots, Material usedMaterial, int shaderId)
            {
                this.usedTextureArraySlots = usedTextureArraySlots;
                this.usedMaterial = usedMaterial;
                this.shaderId = shaderId;
            }
        }

        public static readonly ListExtendedObjectPool<MaterialSetup> USED_SLOTS_POOL = new (listInstanceDefaultCapacity: 10, defaultCapacity: PoolConstants.AVATARS_COUNT);

        /// <summary>
        ///     Acquired Region of the common buffer, it may change during defragmentation
        /// </summary>
        public FixedComputeBufferHandler.Slice VertsOutRegion;

        internal readonly int vertCount;

        internal Buffers buffers;
        internal readonly List<MaterialSetup> materials;
        internal readonly UnityEngine.ComputeShader computeShaderInstance;

        private bool disposed;

        internal AvatarCustomSkinningComponent(int vertCount, Buffers buffers, List<MaterialSetup> materials, UnityEngine.ComputeShader computeShaderInstance)
        {
            this.vertCount = vertCount;
            this.buffers = buffers;
            this.materials = materials;
            this.computeShaderInstance = computeShaderInstance;
            VertsOutRegion = default(FixedComputeBufferHandler.Slice);

            disposed = false;
        }

        public void Dispose(IAvatarMaterialPoolHandler objectPool, IObjectPool<UnityEngine.ComputeShader> computeShaderSkinningPool)
        {
            if (CheckIfDisposed())
                return;

            for (var i = 0; i < materials.Count; i++)
            {
                MaterialSetup material = materials[i];

                for (var j = 0; j < material.usedTextureArraySlots.Length; j++)
                    material.usedTextureArraySlots[j]?.FreeSlot();

                objectPool.Release(material);
            }

            computeShaderSkinningPool.Release(computeShaderInstance);

            buffers.DisposeBuffers();
            USED_SLOTS_POOL.Release(materials);

            disposed = true;
        }

        private bool CheckIfDisposed()
        {
            if (!disposed) return false;

            ReportHub.LogError(ReportCategory.AVATAR, $"{nameof(AvatarCustomSkinningComponent)} is already disposed");
            return true;
        }
    }
}
