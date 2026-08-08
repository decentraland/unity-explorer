using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DCL.AvatarRendering.AvatarShape.ComputeShader
{
    [BurstCompile]
    public struct BoneMatrixCalculationJob : IJobParallelFor, IDisposable
    {
        // Per-avatar slot stride in the flat matrix buffers (MAX_BONE_COUNT). It only fixes WHERE each
        // avatar's block starts (offset = avatarIdx * boneStride) and must stay in lock-step with the
        // stride the consumer reads from: AvatarCustomSkinningComponent.ComputeSkinning uploads from
        // validIndex * MAX_BONE_COUNT. It is NOT the number of matrices to produce — that is per-avatar
        // (see PerAvatarBoneCount).
        private readonly int boneStride;

        [NativeDisableParallelForRestriction]
        private NativeArray<float4x4> bonesMatricesResult;
        [NativeDisableParallelForRestriction]
        public NativeArray<float4x4> AvatarTransform;
        [NativeDisableParallelForRestriction]
        private NativeArray<float4x4> boneWorldMatrixArray;

        [NativeDisableParallelForRestriction] public NativeArray<bool> UpdateAvatar;

        // Number of matrices to produce for each avatar, indexed by avatar slot. This is the SAME
        // authoritative count the consumer uploads to the GPU — AvatarCustomSkinningComponent.BoneCount
        // (bones.SetData(bonesResult, validIndex * MAX_BONE_COUNT, 0, BoneCount)) — refreshed here every
        // frame before Schedule. Producing exactly this range (and clamping it to boneStride) guarantees
        // ComputeSkinning never reads an uncomputed/stale tail slot, while the [BoneCount, boneStride)
        // padding slots — which are never uploaded — are skipped instead of recomputed every frame.
        [ReadOnly] [NativeDisableParallelForRestriction] public NativeArray<int> PerAvatarBoneCount;

        public NativeArray<float4x4> BonesMatricesResult => bonesMatricesResult;

        public BoneMatrixCalculationJob(int boneStride, int bonesPerAvatarLength, NativeArray<float4x4> boneWorldMatrixArray)
        {
            this.boneStride = boneStride;
            bonesMatricesResult = new NativeArray<float4x4>(bonesPerAvatarLength, Allocator.Persistent);
            AvatarTransform = default;
            UpdateAvatar = default;
            PerAvatarBoneCount = default;

            this.boneWorldMatrixArray = boneWorldMatrixArray;
        }

        public void Dispose()
        {
            bonesMatricesResult.Dispose();
        }

        // Each parallel task handles one avatar: the UpdateAvatar check runs once, AvatarTransform is
        // loaded once, and the inner bone loop is a tight sequential range that Burst can auto-vectorize.
        // Only the first PerAvatarBoneCount[avatarIdx] matrices are produced — exactly the range the
        // consumer uploads — clamped to boneStride so a slot can never spill into the next avatar's block.
        public void Execute(int avatarIdx)
        {
            if (!UpdateAvatar[avatarIdx])
                return;

            float4x4 avatarMatrix = AvatarTransform[avatarIdx];
            int offset = avatarIdx * boneStride;
            int count = math.min(PerAvatarBoneCount[avatarIdx], boneStride);

            for (int b = 0; b < count; b++)
                bonesMatricesResult[offset + b] = math.mul(avatarMatrix, boneWorldMatrixArray[offset + b]);
        }
    }
}
