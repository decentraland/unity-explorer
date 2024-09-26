using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace DCL.AvatarRendering.AvatarShape.ComputeShader
{
    [BurstCompile]
    public struct BoneMatrixCalculationJob : IJobParallelForTransform
    {
        private readonly int BONE_COUNT;
        private int AvatarIndex;

        public NativeArray<float4x4> BonesMatricesResult;
        [NativeDisableParallelForRestriction]
        public NativeArray<Matrix4x4> AvatarTransform;

        [NativeDisableParallelForRestriction] public NativeArray<bool> UpdateAvatar;

        public BoneMatrixCalculationJob(int boneCount, int bonesPerAvatarLength)
        {
            BONE_COUNT = boneCount;
            BonesMatricesResult = new NativeArray<float4x4>(bonesPerAvatarLength, Allocator.Persistent);
            AvatarTransform = default;
            UpdateAvatar = default;
            AvatarIndex = 0;
        }

        public void Execute(int index, TransformAccess transform)
        {
            // The avatarIndex is calculated by dividing the index by the amount of bones per avatar
            // Therefore, all of the indexes between 0 and ComputeShaderConstants.BONE_COUNT correlates to a single avatar
            AvatarIndex = index / BONE_COUNT;
            if (!UpdateAvatar[AvatarIndex])
                return;
            BonesMatricesResult[index] = AvatarTransform[AvatarIndex] * transform.localToWorldMatrix;
        }
    }
}
