using System;
using System.Collections.Generic;
using System.Linq;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    public unsafe class AvatarTransformMatrixJobWrapper : IDisposable
    {
        private NativeArray<Matrix4x4> MatrixFromAllAvatars;
        private Matrix4x4* matrixPtr;
        
        private TransformAccessArray bonesCombined;
        public BoneMatrixCalculationJob job;

        private JobHandle handle;

        internal bool disposed { get;  }
        internal bool completed { get; private set; }

        private int AvatarIndex;
        private static int BONES_PER_AVATAR_LENGTH;
        private static readonly int BONES_ARRAY_LENGTH = ComputeShaderConstants.BONE_COUNT;
        private const int TOTAL_AVATAR_SUPPORTED = 100;


        public static AvatarTransformMatrixJobWrapper Create()
        {
            BONES_PER_AVATAR_LENGTH = TOTAL_AVATAR_SUPPORTED * BONES_ARRAY_LENGTH;
            var bonesCombined = new TransformAccessArray(BONES_PER_AVATAR_LENGTH);
            var matrixFromAllAvatars = new NativeArray<Matrix4x4>(BONES_PER_AVATAR_LENGTH, Allocator.Persistent);
            for (int i = 0; i < BONES_PER_AVATAR_LENGTH; i++)
                bonesCombined.Add(null);
            return new AvatarTransformMatrixJobWrapper
            {
                MatrixFromAllAvatars = matrixFromAllAvatars, matrixPtr = (Matrix4x4*)matrixFromAllAvatars.GetUnsafePtr(),
                bonesCombined = bonesCombined, job = new BoneMatrixCalculationJob
                {
                    BonesMatricesResult = new NativeArray<float4x4>(BONES_PER_AVATAR_LENGTH, Allocator.Persistent)
                }
            };
        }

        public void ScheduleBoneMatrixCalculation()
        {
            job.EndIndex = (AvatarIndex + 1) * BONES_ARRAY_LENGTH;
            job.AvatarTransform = MatrixFromAllAvatars;
            handle = job.Schedule(bonesCombined);
            completed = false;
        }

        public void CompleteBoneMatrixCalculations()
        {
            handle.Complete();
            completed = true;
        }

        public void UpdateAvatar(ref AvatarBase avatarBase, ref AvatarTransformMatrixComponent transformMatrixComponent)
        {
            if (transformMatrixComponent.IndexInGlobalJobArray == -1)
            {
                //Add all bones to the bonesCombined array with the current available index
                for (int i = 0; i < BONES_ARRAY_LENGTH; i++)
                    bonesCombined[AvatarIndex * BONES_ARRAY_LENGTH + i] = transformMatrixComponent.bones[i];
                //Set the index for this avatar
                transformMatrixComponent.IndexInGlobalJobArray = AvatarIndex;
                AvatarIndex++;
            }

            //Setup of data
            matrixPtr[transformMatrixComponent.IndexInGlobalJobArray] = avatarBase.transform.worldToLocalMatrix;
        }

        public void Dispose()
        {
            handle.Complete();
            bonesCombined.Dispose();
            job.BonesMatricesResult.Dispose();
        }
    }
}