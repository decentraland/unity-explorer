using System;
using System.Collections.Generic;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    public class AvatarTransformMatrixJobWrapper : IDisposable
    {
        private List<TransformAccessArray> BonesFromAllAvatars;
        private NativeArray<Matrix4x4> MatrixFromAllAvatars;

        private TransformAccessArray bonesCombined;
        private BoneMatrixCalculationJob job;
        private JobHandle handle;

        internal bool disposed { get;  }
        internal bool completed { get; private set; }

        private int AvatarIndex;
        private const int BONES_ARRAY_LENGTH = 62;
        private const int TOTAL_AVATAR_SUPPORTED = 100;

        public static AvatarTransformMatrixJobWrapper Create()
        {
            return new AvatarTransformMatrixJobWrapper
            {
                BonesFromAllAvatars = new List<TransformAccessArray>(), MatrixFromAllAvatars = new NativeArray<Matrix4x4>(TOTAL_AVATAR_SUPPORTED * BONES_ARRAY_LENGTH, Allocator.Persistent), job = new BoneMatrixCalculationJob
                {
                    BonesMatricesResult = new NativeArray<float4x4>(TOTAL_AVATAR_SUPPORTED * BONES_ARRAY_LENGTH, Allocator.Persistent), BonesLength = BONES_ARRAY_LENGTH
                }
            };
        }

        public void Reset()
        {
            AvatarIndex = 0;
            BonesFromAllAvatars.Clear();
        }

        public void ScheduleBoneMatrixCalculation()
        {
            bonesCombined = new TransformAccessArray(BonesFromAllAvatars.Count * BONES_ARRAY_LENGTH);
            foreach (var array in BonesFromAllAvatars)
            {
                for (int i = 0; i < array.length; i++)
                    bonesCombined.Add(array[i]);
            }

            job.AvatarTransform = MatrixFromAllAvatars;
            handle = job.Schedule(bonesCombined);
            completed = false;
        }

        public void CompleteBoneMatrixCalculations()
        {
            handle.Complete();
            completed = true;
        }

        public int AddAvatar(ref AvatarBase avatarBase, ref AvatarTransformMatrixComponent transformMatrixComponent)
        {
            BonesFromAllAvatars.Add(transformMatrixComponent.bones);
            for (int i = 0; i < BONES_ARRAY_LENGTH; i++)
                MatrixFromAllAvatars[AvatarIndex * BONES_ARRAY_LENGTH + i] = avatarBase.transform.worldToLocalMatrix;
            return AvatarIndex++;
        }

        public NativeArray<float4x4> GetResultForIndex(int index)
        {
            return job.BonesMatricesResult.GetSubArray(index * BONES_ARRAY_LENGTH, BONES_ARRAY_LENGTH);
        }

        public void Dispose()
        {
            handle.Complete();
            bonesCombined.Dispose();
            job.BonesMatricesResult.Dispose();
        }
    }
}