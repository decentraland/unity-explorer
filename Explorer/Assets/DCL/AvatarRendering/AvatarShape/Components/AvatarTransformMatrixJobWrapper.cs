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
        

        private NativeArray<Matrix4x4> BufferArrayForBulkCopy;
        private Matrix4x4* BufferArrayForBulkCopyPtr;

        private TransformAccessArray bonesCombined;
        private BoneMatrixCalculationJob job;
        private JobHandle handle;

        internal bool disposed { get;  }
        internal bool completed { get; private set; }

        private int AvatarIndex;
        private static int BONES_PER_AVATAR_LENGTH;
        private const int BONES_ARRAY_LENGTH = 62;
        private const int TOTAL_AVATAR_SUPPORTED = 100;

        private readonly Dictionary<int, int> AvatarToIndexDictionary = new ();

        // Calculate the size of the memory block you want to copy
        private readonly int sizeToCopy = sizeof(Matrix4x4) * BONES_ARRAY_LENGTH;

        public static AvatarTransformMatrixJobWrapper Create()
        {
            BONES_PER_AVATAR_LENGTH = TOTAL_AVATAR_SUPPORTED * BONES_ARRAY_LENGTH;
            var bonesCombined = new TransformAccessArray(BONES_PER_AVATAR_LENGTH);
            var matrixFromAllAvatars = new NativeArray<Matrix4x4>(BONES_PER_AVATAR_LENGTH, Allocator.Persistent);
            var tempArrayForCopying = new NativeArray<Matrix4x4>(BONES_ARRAY_LENGTH, Allocator.Persistent);

            for (int i = 0; i < BONES_PER_AVATAR_LENGTH; i++)
                bonesCombined.Add(null);
            return new AvatarTransformMatrixJobWrapper
            {
                MatrixFromAllAvatars = matrixFromAllAvatars, matrixPtr = (Matrix4x4*)matrixFromAllAvatars.GetUnsafePtr(), BufferArrayForBulkCopy = tempArrayForCopying, BufferArrayForBulkCopyPtr = (Matrix4x4*)tempArrayForCopying.GetUnsafePtr(),
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

        public  int AddAvatar(ref AvatarBase avatarBase, ref AvatarTransformMatrixComponent transformMatrixComponent)
        {
            if (!AvatarToIndexDictionary.TryGetValue(avatarBase.RandomID, out int currentIndex))
            {
                AvatarToIndexDictionary.Add(avatarBase.RandomID, AvatarIndex);
                int indexInDictionary = AvatarIndex * BONES_ARRAY_LENGTH;
                for (int i = 0; i < BONES_ARRAY_LENGTH; i++)
                {
                    bonesCombined[indexInDictionary + i] = transformMatrixComponent.bones[i];
                }

                currentIndex = AvatarIndex;
                AvatarIndex++;
            }

            //Setup of data
            matrixPtr[currentIndex] = avatarBase.transform.worldToLocalMatrix;
            return currentIndex;
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