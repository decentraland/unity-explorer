using System;
using System.Collections.Generic;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    public unsafe class AvatarTransformMatrixJobWrapper : IDisposable
    {
        private const int INNER_LOOP_BATCH_COUNT = 128; // Each iteration is lightweight. Reduces overhead from frequent job switching.

        internal const int AVATAR_ARRAY_SIZE = 100;
        private static readonly int BONES_ARRAY_LENGTH = ComputeShaderConstants.BONE_COUNT;
        private static readonly int BONES_PER_AVATAR_LENGTH = AVATAR_ARRAY_SIZE * BONES_ARRAY_LENGTH;

        internal NativeArray<Matrix4x4> matrixFromAllAvatars;
        private Matrix4x4* matrixPtr;

        internal NativeArray<bool> updateAvatar;
        private bool* updateAvatarPtr;

        private NativeArray<Matrix4x4> bonesCombined;
        public BoneMatrixCalculationJob job;

        private JobHandle handle;

        private readonly Stack<int> releasedIndexes;

        private int avatarIndex;
        private int nextResizeValue;
        internal int currentAvatarAmountSupported;

        //Helper transform for bone matrix calculation
        private readonly Matrix4x4 identityTransform;

        public AvatarTransformMatrixJobWrapper()
        {
            bonesCombined = new NativeArray<Matrix4x4>(BONES_PER_AVATAR_LENGTH, Allocator.Persistent);
            identityTransform = new GameObject("Identity_Helper_Transform").transform.localToWorldMatrix;

            job = new BoneMatrixCalculationJob(BONES_ARRAY_LENGTH, BONES_PER_AVATAR_LENGTH, bonesCombined);

            for (int i = 0; i < BONES_PER_AVATAR_LENGTH; i++)
                bonesCombined[i] = identityTransform;

            matrixFromAllAvatars
                = new NativeArray<Matrix4x4>(AVATAR_ARRAY_SIZE, Allocator.Persistent);

            matrixPtr = (Matrix4x4*)matrixFromAllAvatars.GetUnsafePtr();

            updateAvatar = new NativeArray<bool>(AVATAR_ARRAY_SIZE, Allocator.Persistent);
            updateAvatarPtr = (bool*)updateAvatar.GetUnsafePtr();

            currentAvatarAmountSupported = AVATAR_ARRAY_SIZE;

            nextResizeValue = 2;
            releasedIndexes = new Stack<int>();
        }

        public void ScheduleBoneMatrixCalculation()
        {
            job.AvatarTransform = matrixFromAllAvatars;
            job.UpdateAvatar = updateAvatar;
            handle = job.Schedule(ActiveBonesCount(), INNER_LOOP_BATCH_COUNT);
        }

        public void CompleteBoneMatrixCalculations()
        {
            handle.Complete();
        }

        public void UpdateAvatar(AvatarBase avatarBase, ref AvatarTransformMatrixComponent transformMatrixComponent)
        {
            if (transformMatrixComponent.IndexInGlobalJobArray == -1)
            {
                if (releasedIndexes.Count > 0)
                    transformMatrixComponent.IndexInGlobalJobArray = releasedIndexes.Pop();
                else
                {
                    transformMatrixComponent.IndexInGlobalJobArray = avatarIndex;
                    avatarIndex++;
                }
            }

            //Add all bones to the bonesCombined array with the current available index
            for (int i = 0; i < BONES_ARRAY_LENGTH; i++)
                bonesCombined[transformMatrixComponent.IndexInGlobalJobArray * BONES_ARRAY_LENGTH + i] =
                    transformMatrixComponent.bones[i].localToWorldMatrix;

            //Setup of data
            matrixPtr[transformMatrixComponent.IndexInGlobalJobArray] = avatarBase.transform.worldToLocalMatrix;
            updateAvatarPtr[transformMatrixComponent.IndexInGlobalJobArray] = true;

            if (avatarIndex >= currentAvatarAmountSupported - 1)
                ResizeArrays();
        }

        private void ResizeArrays()
        {
            var newBonesCombined
                = new NativeArray<Matrix4x4>(BONES_PER_AVATAR_LENGTH * nextResizeValue, Allocator.Persistent);

            for (var i = 0; i < BONES_PER_AVATAR_LENGTH * nextResizeValue; i++)
                newBonesCombined[i] = i < BONES_PER_AVATAR_LENGTH * (nextResizeValue - 1)
                    ? bonesCombined[i]
                    : identityTransform;

            bonesCombined.Dispose();
            bonesCombined = newBonesCombined;

            var newMatrixFromAllAvatars
                = new NativeArray<Matrix4x4>(AVATAR_ARRAY_SIZE * nextResizeValue, Allocator.Persistent);

            UnsafeUtility.MemCpy(newMatrixFromAllAvatars.GetUnsafePtr(), matrixFromAllAvatars.GetUnsafePtr(),
                matrixFromAllAvatars.Length * sizeof(Matrix4x4));

            matrixFromAllAvatars.Dispose();
            matrixFromAllAvatars = newMatrixFromAllAvatars;
            matrixPtr = (Matrix4x4*)matrixFromAllAvatars.GetUnsafePtr();

            var newUpdateAvatar
                = new NativeArray<bool>(AVATAR_ARRAY_SIZE * nextResizeValue, Allocator.Persistent);

            UnsafeUtility.MemCpy(newUpdateAvatar.GetUnsafePtr(), updateAvatar.GetUnsafePtr(),
                updateAvatar.Length * sizeof(bool));

            updateAvatar.Dispose();
            updateAvatar = newUpdateAvatar;
            updateAvatarPtr = (bool*)updateAvatar.GetUnsafePtr();

            job.BonesMatricesResult.Dispose();
            job = new BoneMatrixCalculationJob(BONES_ARRAY_LENGTH, BONES_PER_AVATAR_LENGTH * nextResizeValue, bonesCombined);

            currentAvatarAmountSupported = AVATAR_ARRAY_SIZE * nextResizeValue;
            nextResizeValue++;
        }

        private int ActiveBonesCount() =>
            avatarIndex * BONES_ARRAY_LENGTH;

        public void Dispose()
        {
            handle.Complete();
            bonesCombined.Dispose();
            updateAvatar.Dispose();
            job.BonesMatricesResult.Dispose();
        }

        public void ReleaseAvatar(ref AvatarTransformMatrixComponent avatarTransformMatrixComponent)
        {
            if (avatarTransformMatrixComponent.IndexInGlobalJobArray == -1)
                return;

            //Dont update this index anymore until reset
            updateAvatarPtr[avatarTransformMatrixComponent.IndexInGlobalJobArray] = false;
            releasedIndexes.Push(avatarTransformMatrixComponent.IndexInGlobalJobArray);

            avatarTransformMatrixComponent.IndexInGlobalJobArray = -1;
        }
    }
}
