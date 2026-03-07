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
        private readonly int boneCount;

        [NativeDisableParallelForRestriction]
        private NativeArray<float4x4> bonesMatricesResult;
        [NativeDisableParallelForRestriction]
        public NativeArray<float4x4> AvatarTransform;
        [NativeDisableParallelForRestriction]
        private NativeArray<float4x4> boneWorldMatrixArray;

        [NativeDisableParallelForRestriction] public NativeArray<bool> UpdateAvatar;

        public NativeArray<float4x4> BonesMatricesResult => bonesMatricesResult;

        public BoneMatrixCalculationJob(int boneCount, int bonesPerAvatarLength, NativeArray<float4x4> boneWorldMatrixArray)
        {
            this.boneCount = boneCount;
            bonesMatricesResult = new NativeArray<float4x4>(bonesPerAvatarLength, Allocator.Persistent);
            AvatarTransform = default;
            UpdateAvatar = default;

            this.boneWorldMatrixArray = boneWorldMatrixArray;
        }

        public void Dispose()
        {
            bonesMatricesResult.Dispose();
        }

        // Each parallel task handles one avatar: the UpdateAvatar check runs once, AvatarTransform is
        // loaded once, and the inner bone loop is a tight sequential range that Burst can auto-vectorize.
        public void Execute(int avatarIdx)
        {
            if (!UpdateAvatar[avatarIdx])
                return;

            float4x4 avatarMatrix = AvatarTransform[avatarIdx];
            int offset = avatarIdx * boneCount;

            for (int b = 0; b < boneCount; b++)
                bonesMatricesResult[offset + b] = math.mul(avatarMatrix, boneWorldMatrixArray[offset + b]);
        }
    }
}
