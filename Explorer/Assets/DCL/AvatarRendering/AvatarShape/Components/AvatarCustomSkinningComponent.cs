using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.Optimization.Pools;
using System.Collections.Generic;
using DCL.Diagnostics;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Assertions;
using RichTypes;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    /// <summary>
    ///     Stores data for the compute shader to perform skinning
    /// </summary>
    public struct AvatarCustomSkinningComponent
    {
        public struct Buffers
        {
            // since it's impossible to guarantee initialization of structure in C# the case requires to provide an additional check
            private ComputeSkinningBufferContainer computeSkinningBufferContainer;
            private readonly ComputeBuffer bones; 
            internal readonly int kernel;

            public Buffers(ComputeBuffer bones, int kernel) : this()
            {
                Assert.IsNotNull(bones, "AvatarCustomSkinningComponent cannot have NULL ComputeBuffer as bones");
                this.bones = bones;
                this.kernel = kernel;
            }

            public bool TryGetBones(out ComputeBuffer bones)
            {
                bones = this.bones;
                return bones != null;
            }

            public void AssignBuffer(ComputeSkinningBufferContainer container)
            {
                computeSkinningBufferContainer = container;
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

        public readonly int VertCount;

        internal readonly List<MaterialSetup> materials;
        private readonly Buffers buffers;
        private readonly UnityEngine.ComputeShader computeShaderInstance;

        private bool disposed;

        /// <summary>
        ///
        /// </summary>
        public Bounds LocalBounds { get; private set; }

        internal AvatarCustomSkinningComponent(int vertCount, Buffers buffers, List<MaterialSetup> materials, UnityEngine.ComputeShader computeShaderInstance, Bounds localBounds)
        {
            VertCount = vertCount;
            this.buffers = buffers;
            this.materials = materials;
            this.computeShaderInstance = computeShaderInstance;
            this.LocalBounds = localBounds;
            VertsOutRegion = default(FixedComputeBufferHandler.Slice);

            disposed = false;
        }

        /// <summary>
        /// Changes the Fading Position parameter of all the materials of the meshes that form the avatar, including facial features.
        /// This parameter is used for computing the transparency of the avatar regarding the distance of the camera to it.
        /// </summary>
        /// <param name="position">The reference position used for computing the distance from the avatar to the camera.</param>
        public readonly void SetFadingDistance(float distance)
        {
            for (int i = 0; i < materials.Count; ++i)
            {
                materials[i].usedMaterial.SetFloat(ComputeShaderConstants.SHADER_FADING_DISTANCE_PARAM_ID, distance);
            }
        }

        public Result ComputeSkinning(NativeArray<float4x4> bonesResult, GlobalJobArrayIndex indexInGlobalJobArray)
        {
            if (indexInGlobalJobArray.TryGetValue(out int validIndex) == false)
            {
                return Result.ErrorResult("Attempt to process an invalid avatar");
            }

            if (buffers.TryGetBones(out ComputeBuffer bones) == false)
            {
                return Result.ErrorResult("ComputeSkinning error: Cannot get bones (ComputeBuffer)");
            }

            bones.SetData(bonesResult, validIndex * ComputeShaderConstants.BONE_COUNT, 0 , ComputeShaderConstants.BONE_COUNT);
            computeShaderInstance.Dispatch(buffers.kernel, (VertCount / 64) + 1, 1, 1);
            return Result.SuccessResult();

            //Note (Juani): According to Unity, BeginWrite/EndWrite works better than SetData. But we got inconsitent result using ComputeBufferMode.SubUpdates
            //Ash machine (AMD) worked way worse than mine (NVidia). So, we are back to SetData with a ComputeBufferMode.Dynamic, which works well for both.
            //https://docs.unity3d.com/2020.1/Documentation/ScriptReference/ComputeBuffer.BeginWrite.html
            /*NativeArray<float4x4> bonesIn = mBones.BeginWrite<float4x4>(0, ComputeShaderConstants.BONE_COUNT);
            NativeArray<float4x4>.Copy(bonesResult, 0, bonesIn, 0, ComputeShaderConstants.BONE_COUNT);
            mBones.EndWrite<float4x4>(ComputeShaderConstants.BONE_COUNT);*/
        }

        public void SetVertOutRegion(FixedComputeBufferHandler.Slice region)
        {
            VertsOutRegion = region;

            computeShaderInstance.SetInt(ComputeShaderConstants.LAST_AVATAR_VERT_COUNT_ID, region.StartIndex);

            for (var i = 0; i < materials.Count; i++)
                materials[i].usedMaterial.SetInteger(ComputeShaderConstants.LAST_AVATAR_VERT_COUNT_ID, region.StartIndex);
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
