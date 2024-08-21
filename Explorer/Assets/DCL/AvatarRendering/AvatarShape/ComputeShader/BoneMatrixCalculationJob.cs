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
        public NativeArray<Matrix4x4> AvatarTransform;
        public int BonesLength;

        public void Execute(int index, TransformAccess transform)
        {
            //n amount of bones per avatar
            //BonesMatricesResult[index] = AvatarTransform[index / BonesLength] * transform.localToWorldMatrix;
            BonesMatricesResult[index] = AvatarTransform[index] * transform.localToWorldMatrix;
        }
    }
}
