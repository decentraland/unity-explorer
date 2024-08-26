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

        public void Execute(int index, TransformAccess transform)
        {
            if (!UpdateAvatar[index / 62])
                return;
            BonesMatricesResult[index] = AvatarTransform[index / 62] * transform.localToWorldMatrix;
        }
    }
}
