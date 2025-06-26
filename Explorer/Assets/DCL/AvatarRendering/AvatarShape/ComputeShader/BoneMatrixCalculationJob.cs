using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.ComputeShader
{
    [BurstCompile]
    public struct BoneMatrixCalculationJob : IJobParallelFor
    {
        private readonly int BONE_COUNT;
        private int AvatarIndex;

        public NativeArray<float4x4> BonesMatricesResult;
        [NativeDisableParallelForRestriction]
        public NativeArray<Matrix4x4> AvatarTransform;

        private NativeArray<Matrix4x4> boneWorldMatrixArray;

        [NativeDisableParallelForRestriction] public NativeArray<bool> UpdateAvatar;

        public BoneMatrixCalculationJob(int boneCount, int bonesPerAvatarLength, NativeArray<Matrix4x4> boneWorldMatrixArray)
        {
            BONE_COUNT = boneCount;
            BonesMatricesResult = new NativeArray<float4x4>(bonesPerAvatarLength, Allocator.Persistent);
            AvatarTransform = default;
            UpdateAvatar = default;
            AvatarIndex = 0;

            this.boneWorldMatrixArray = boneWorldMatrixArray;
        }

        public void Execute(int index)
        {
            // The avatarIndex is calculated by dividing the index by the amount of bones per avatar
            // Therefore, all of the indexes between 0 and ComputeShaderConstants.BONE_COUNT correlates to a single avatar
            AvatarIndex = index / BONE_COUNT;

            if (!UpdateAvatar[AvatarIndex])
                return;

            BonesMatricesResult[index] = AvatarTransform[AvatarIndex] * boneWorldMatrixArray[index];
        }
    }
}
