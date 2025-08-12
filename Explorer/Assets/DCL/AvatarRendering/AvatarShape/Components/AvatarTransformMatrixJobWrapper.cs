using System;
using System.Collections.Generic;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Profiling;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    public class AvatarTransformMatrixJobWrapper : IDisposable
    {
        private const int INNER_LOOP_BATCH_COUNT = 128; // Each iteration is lightweight. Reduces overhead from frequent job switching.
        private const int WORLD_MATRIX_CALCULATION_STRATEGY_LIMIT = 128; // There is no gain in performance to multithreading for less amount of avatars

        internal const int AVATAR_ARRAY_SIZE = 100;
        private static readonly int BONES_ARRAY_LENGTH = ComputeShaderConstants.BONE_COUNT;
        private static readonly int BONES_PER_AVATAR_LENGTH = AVATAR_ARRAY_SIZE * BONES_ARRAY_LENGTH;

        private NativeArray<Matrix4x4> matrixFromAllAvatars;
        private NativeArray<bool> updateAvatar;

        private TransformAccessArray copyBufferPerAvatar;

        private NativeArray<Matrix4x4> bonesCombined;
        public BoneMatrixCalculationJob job;

        private JobHandle handle;
        private bool disposed;

        private readonly Stack<int> releasedIndexes;

        private int avatarIndex;
        private int nextResizeValue;
        internal int currentAvatarAmountSupported;

#if UNITY_INCLUDE_TESTS
        public NativeArray<Matrix4x4>.ReadOnly MatrixFromAllAvatars => matrixFromAllAvatars.AsReadOnly();
        public NativeArray<bool>.ReadOnly UpdateAvatarValue => updateAvatar.AsReadOnly();
#endif

        public AvatarTransformMatrixJobWrapper()
        {
            bonesCombined = new NativeArray<Matrix4x4>(BONES_PER_AVATAR_LENGTH, Allocator.Persistent);
            copyBufferPerAvatar = new TransformAccessArray(BONES_ARRAY_LENGTH);

            job = new BoneMatrixCalculationJob(BONES_ARRAY_LENGTH, BONES_PER_AVATAR_LENGTH, bonesCombined);

            matrixFromAllAvatars = new NativeArray<Matrix4x4>(AVATAR_ARRAY_SIZE, Allocator.Persistent);
            updateAvatar = new NativeArray<bool>(AVATAR_ARRAY_SIZE, Allocator.Persistent);

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

            Profiler.BeginSample("Calculate localToWorldMatrix");

            int globalIndexOffset = transformMatrixComponent.IndexInGlobalJobArray * BONES_ARRAY_LENGTH;

            if (avatarIndex < WORLD_MATRIX_CALCULATION_STRATEGY_LIMIT)
            {
                Profiler.BeginSample("Calculate localToWorldMatrix on MainThread");

                //Add all bones to the bonesCombined array with the current available index
                for (int i = 0; i < BONES_ARRAY_LENGTH; i++)
                    bonesCombined[globalIndexOffset + i] = transformMatrixComponent.bones[i].localToWorldMatrix;

                Profiler.EndSample();
            }
            else
            {
                Profiler.BeginSample("Calculate localToWorldMatrix on Job");

                copyBufferPerAvatar.SetTransforms(transformMatrixComponent.bones.Inner);
                var worldMatrixJob = new WorldMatrixCalculationJob(bonesCombined, globalIndexOffset);
                worldMatrixJob.Schedule(copyBufferPerAvatar).Complete();

                Profiler.EndSample();
            }

            Profiler.EndSample();

            //Setup of data
            matrixFromAllAvatars[transformMatrixComponent.IndexInGlobalJobArray] = avatarBase.transform.worldToLocalMatrix;
            updateAvatar[transformMatrixComponent.IndexInGlobalJobArray] = true;

            if (avatarIndex >= currentAvatarAmountSupported - 1)
                ResizeArrays();
        }

        private unsafe void ResizeArrays()
        {
            var newBonesCombined
                = new NativeArray<Matrix4x4>(BONES_PER_AVATAR_LENGTH * nextResizeValue, Allocator.Persistent);

            int copyCount = BONES_PER_AVATAR_LENGTH * (nextResizeValue - 1);
            long bytesToCopy = copyCount * UnsafeUtility.SizeOf<Matrix4x4>();

            UnsafeUtility.MemCpy(
                destination: newBonesCombined.GetUnsafePtr()!,
                source: bonesCombined.GetUnsafeReadOnlyPtr()!,
                size: bytesToCopy
            );

            bonesCombined.Dispose();
            bonesCombined = newBonesCombined;

            var newMatrixFromAllAvatars
                = new NativeArray<Matrix4x4>(AVATAR_ARRAY_SIZE * nextResizeValue, Allocator.Persistent);

            UnsafeUtility.MemCpy(newMatrixFromAllAvatars.GetUnsafePtr(), matrixFromAllAvatars.GetUnsafePtr(),
                matrixFromAllAvatars.Length * sizeof(Matrix4x4));

            matrixFromAllAvatars.Dispose();
            matrixFromAllAvatars = newMatrixFromAllAvatars;

            var newUpdateAvatar
                = new NativeArray<bool>(AVATAR_ARRAY_SIZE * nextResizeValue, Allocator.Persistent);

            UnsafeUtility.MemCpy(newUpdateAvatar.GetUnsafePtr(), updateAvatar.GetUnsafePtr(),
                updateAvatar.Length * sizeof(bool));

            updateAvatar.Dispose();
            updateAvatar = newUpdateAvatar;

            job.BonesMatricesResult.Dispose();
            job = new BoneMatrixCalculationJob(BONES_ARRAY_LENGTH, BONES_PER_AVATAR_LENGTH * nextResizeValue, bonesCombined);

            currentAvatarAmountSupported = AVATAR_ARRAY_SIZE * nextResizeValue;
            nextResizeValue++;
        }

        private int ActiveBonesCount() =>
            avatarIndex * BONES_ARRAY_LENGTH;

        public void Dispose()
        {
            disposed = true;

            handle.Complete();
            bonesCombined.Dispose();
            updateAvatar.Dispose();
            job.BonesMatricesResult.Dispose();
        }

        public void ReleaseAvatar(ref AvatarTransformMatrixComponent avatarTransformMatrixComponent)
        {
            if (disposed) return;
            if (avatarTransformMatrixComponent.IndexInGlobalJobArray == -1) return;

            //Dont update this index anymore until reset
            updateAvatar[avatarTransformMatrixComponent.IndexInGlobalJobArray] = false;
            releasedIndexes.Push(avatarTransformMatrixComponent.IndexInGlobalJobArray);

            avatarTransformMatrixComponent.IndexInGlobalJobArray = -1;
        }
    }
}
