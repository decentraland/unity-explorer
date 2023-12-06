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
        public Matrix4x4 AvatarTransform;

        public void Execute(int index, TransformAccess transform)
        {
            BonesMatricesResult[index] = AvatarTransform * transform.localToWorldMatrix;
        }
    }
}
