using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.Rendering.Avatar;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using Utility.Pool;

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

        internal struct MaterialSetup
        {
            internal readonly TextureArraySlot? usedTextureArraySlot;
            /// <summary>
            ///     Cel Shading Material is created based on the original material
            /// </summary>
            internal readonly Material celShadingMaterial;

            public MaterialSetup(TextureArraySlot? usedTextureArraySlot, Material celShadingMaterial)
            {
                this.usedTextureArraySlot = usedTextureArraySlot;
                this.celShadingMaterial = celShadingMaterial;
            }
        }

        internal static readonly ListObjectPool<MaterialSetup> USED_SLOTS_POOL = new (listInstanceDefaultCapacity: 10, defaultCapacity: PoolConstants.AVATARS_COUNT);

        /// <summary>
        ///     Acquired Region of the common buffer, it may change during defragmentation
        /// </summary>
        public FixedComputeBufferHandler.Slice VertsOutRegion;

        internal readonly int vertCount;

        internal readonly Buffers buffers;
        internal readonly List<MaterialSetup> materials;
        internal readonly UnityEngine.ComputeShader computeShaderInstance;

        internal AvatarCustomSkinningComponent(int vertCount, Buffers buffers, List<MaterialSetup> materials, UnityEngine.ComputeShader computeShaderInstance)
        {
            this.vertCount = vertCount;
            this.buffers = buffers;
            this.materials = materials;
            this.computeShaderInstance = computeShaderInstance;
            VertsOutRegion = default(FixedComputeBufferHandler.Slice);
        }

        public void Dispose(IObjectPool<Material> objectPool)
        {
            for (var i = 0; i < materials.Count; i++)
            {
                materials[i].usedTextureArraySlot?.FreeSlot();

                // objectPool.Release(materials[i].celShadingMaterial);
                UnityObjectUtils.SafeDestroy(materials[i].celShadingMaterial);
            }

            buffers.DisposeBuffers();
            USED_SLOTS_POOL.Release(materials);
        }
    }
}
