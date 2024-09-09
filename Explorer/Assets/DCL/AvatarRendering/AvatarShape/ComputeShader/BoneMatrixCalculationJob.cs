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
        public NativeArray<float4x4> BonesMatricesResult;

        [NativeDisableParallelForRestriction]
        public NativeArray<Matrix4x4> AvatarTransform;

        [NativeDisableParallelForRestriction] public NativeArray<bool> UpdateAvatar;
        private int avatarIndex;

        public void Execute(int index, TransformAccess transform)
        {
            // The avatarIndex is calculated by dividing the index by the amount of bones per avatar
            // Therefore, all of the indexes between 0 and ComputeShaderConstants.BONE_COUNT correlates to a single avatar
            avatarIndex = index / ComputeShaderConstants.BONE_COUNT;
            if (!UpdateAvatar[avatarIndex])
                return;
            BonesMatricesResult[index] = AvatarTransform[avatarIndex] * transform.localToWorldMatrix;
        }
    }
}
