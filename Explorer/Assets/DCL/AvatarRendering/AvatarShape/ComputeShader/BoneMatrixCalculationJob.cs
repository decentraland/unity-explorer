using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.ComputeShader
{
    [BurstCompile]
    public struct BoneMatrixCalculationJob : IJobParallelFor, IDisposable
    {
        private readonly int boneCount;

        private NativeArray<float4x4> bonesMatricesResult;
        [NativeDisableParallelForRestriction]
        public NativeArray<Matrix4x4> AvatarTransform;

        private NativeArray<Matrix4x4> boneWorldMatrixArray;

        [NativeDisableParallelForRestriction] public NativeArray<bool> UpdateAvatar;

        public NativeArray<float4x4> BonesMatricesResult => bonesMatricesResult;

        public BoneMatrixCalculationJob(int boneCount, int bonesPerAvatarLength, NativeArray<Matrix4x4> boneWorldMatrixArray)
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

        public void Execute(int index)
        {
            // The avatarIndex is calculated by dividing the index by the amount of bones per avatar
            // Therefore, all of the indexes between 0 and ComputeShaderConstants.BONE_COUNT correlates to a single avatar
            int avatarIndex = index / boneCount;

            if (!UpdateAvatar[avatarIndex])
                return;

            bonesMatricesResult[index] = AvatarTransform[avatarIndex] * boneWorldMatrixArray[index];
        }
    }
}
