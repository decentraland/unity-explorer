using System;
using System.Collections.Generic;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    public class AvatarTransformMatrixJobWrapper : IDisposable
    {
        private bool disposed;

        private const int INNER_LOOP_BATCH_COUNT = 128; // Each iteration is lightweight. Reduces overhead from frequent job switching.

        internal const int AVATAR_ARRAY_SIZE = 100;

        private QuickArray<Matrix4x4> matrixFromAllAvatars;
        private QuickArray<bool> updateAvatar;

        private QuickArray<Matrix4x4> bonesCombined;
        public BoneMatrixCalculationJob job;

        private JobHandle handle;

        private readonly Stack<GlobalJobArrayIndex> releasedIndexes;

        private int avatarIndex;
        private int nextResizeValue;
        private int currentAvatarAmountSupported;
        private int currentBoneStride;

        public int CurrentBoneStride => currentBoneStride;

#if UNITY_INCLUDE_TESTS
        public int MatrixFromAllAvatarsLength => matrixFromAllAvatars.Length;
        public int UpdateAvatarLength => updateAvatar.Length;
        public int CurrentAvatarAmountSupported => currentAvatarAmountSupported;
#endif

        public AvatarTransformMatrixJobWrapper()
        {
            currentBoneStride = ComputeShaderConstants.BASE_BONE_COUNT;
            int bonesPerAvatarLength = AVATAR_ARRAY_SIZE * currentBoneStride;

            bonesCombined = new QuickArray<Matrix4x4>(bonesPerAvatarLength);

            job = new BoneMatrixCalculationJob(currentBoneStride, bonesPerAvatarLength, bonesCombined.InnerNativeArray());

            matrixFromAllAvatars = new QuickArray<Matrix4x4>(AVATAR_ARRAY_SIZE);
            updateAvatar = new QuickArray<bool>(AVATAR_ARRAY_SIZE);

            currentAvatarAmountSupported = AVATAR_ARRAY_SIZE;

            nextResizeValue = 2;
            releasedIndexes = new Stack<GlobalJobArrayIndex>();
        }

        public void ScheduleBoneMatrixCalculation()
        {
            job.AvatarTransform = matrixFromAllAvatars.InnerNativeArray();
            job.UpdateAvatar = updateAvatar.InnerNativeArray();
            handle = job.Schedule(ActiveBonesCount(), INNER_LOOP_BATCH_COUNT);
        }

        public void CompleteBoneMatrixCalculations()
        {
            handle.Complete();
        }

        public void UpdateAvatar(AvatarBase avatarBase, ref AvatarTransformMatrixComponent transformMatrixComponent)
        {
            int avatarBoneCount = transformMatrixComponent.bones.Count;

            if (avatarBoneCount > currentBoneStride)
                ResizeBoneStride(avatarBoneCount);

            if (transformMatrixComponent.IndexInGlobalJobArray.IsValid() == false)
            {
                if (releasedIndexes.Count > 0)
                    transformMatrixComponent.IndexInGlobalJobArray = releasedIndexes.Pop();
                else
                {
                    transformMatrixComponent.IndexInGlobalJobArray = GlobalJobArrayIndex.ValidUnsafe(avatarIndex);
                    avatarIndex++;
                }
            }

            if (transformMatrixComponent.IndexInGlobalJobArray.TryGetValue(out int validIndex) == false)
            {
                ReportHub.LogError(ReportCategory.AVATAR, "Invalid index after direct assignment");
                return;
            }

            Profiler.BeginSample("Calculate localToWorldMatrix on MainThread");

            int globalIndexOffset = validIndex * currentBoneStride;

            //Add all bones to the bonesCombined array with the current available index
            for (int i = 0; i < avatarBoneCount; i++)
                bonesCombined[globalIndexOffset + i] = transformMatrixComponent.bones[i].localToWorldMatrix;

            Profiler.EndSample();

            //Setup of data
            matrixFromAllAvatars[validIndex] = avatarBase.transform.worldToLocalMatrix;
            updateAvatar[validIndex] = true;

            if (avatarIndex >= currentAvatarAmountSupported - 1)
                ResizeArrays();
        }

        private void ResizeBoneStride(int requiredBoneCount)
        {
            currentBoneStride = Math.Min(requiredBoneCount, ComputeShaderConstants.MAX_BONE_COUNT);
            int totalBones = currentBoneStride * currentAvatarAmountSupported;

            bonesCombined.ReAlloc(totalBones);

            job.Dispose();
            job = new BoneMatrixCalculationJob(currentBoneStride, totalBones, bonesCombined.InnerNativeArray());

            // Invalidate all avatars for one frame — data layout changed
            for (int i = 0; i < currentAvatarAmountSupported; i++)
                updateAvatar[i] = false;
        }

        private void ResizeArrays()
        {
            int totalBones = currentBoneStride * AVATAR_ARRAY_SIZE * nextResizeValue;

            bonesCombined.ReAlloc(totalBones);
            matrixFromAllAvatars.ReAlloc(AVATAR_ARRAY_SIZE * nextResizeValue);
            updateAvatar.ReAlloc(AVATAR_ARRAY_SIZE * nextResizeValue);

            job.Dispose();
            job = new BoneMatrixCalculationJob(currentBoneStride, totalBones, bonesCombined.InnerNativeArray());

            currentAvatarAmountSupported = AVATAR_ARRAY_SIZE * nextResizeValue;
            nextResizeValue++;
        }

        private int ActiveBonesCount() =>
            avatarIndex * currentBoneStride;

        public void Dispose()
        {
            handle.Complete();
            bonesCombined.Dispose();
            updateAvatar.Dispose();
            job.Dispose();
            disposed = true;
        }

        public void ReleaseAvatar(ref AvatarTransformMatrixComponent avatarTransformMatrixComponent)
        {
            if (disposed) return;

            if (avatarTransformMatrixComponent.IndexInGlobalJobArray.TryGetValue(out int validIndex) == false)
                return;

            //Dont update this index anymore until reset
            updateAvatar[validIndex] = false;
            releasedIndexes.Push(avatarTransformMatrixComponent.IndexInGlobalJobArray);

            avatarTransformMatrixComponent.IndexInGlobalJobArray = GlobalJobArrayIndex.Unassign();
        }

        /// <summary>
        /// Implementation operates on NativeArray and mitigates runtime checks for elements access. Supports realloc
        /// </summary>
        private unsafe struct QuickArray<T> : IDisposable where T: unmanaged
        {
            private const Allocator ALLOCATOR = Allocator.Persistent;

            private NativeArray<T> array;
            private T* accessPtr;

            public int Length => array.Length;

            public T this[int index]
            {
                get => accessPtr[index];
                set => accessPtr[index] = value;
            }

            public QuickArray(int length)
            {
                Assert.IsTrue(length > 0, "length > 0, length must be greater than 0");
                array = new NativeArray<T>(length, ALLOCATOR);
                accessPtr = (T*)array.GetUnsafePtr();
            }

            /// <summary>
            /// Reallocate to exactly newLength, preserving min(old,new) items.
            /// </summary>
            public void ReAlloc(int newLength, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)
            {
                if (!array.IsCreated)
                {
                    // Fresh allocate
                    array = new NativeArray<T>(newLength, ALLOCATOR, options);
                    accessPtr = (T*)array.GetUnsafePtr();
                    return;
                }

                if (newLength == array.Length) return;

                NativeArray<T> newArray = new NativeArray<T>(newLength, ALLOCATOR, options);

                int count = Mathf.Min(array.Length, newLength);
                long bytesToCopy = count * UnsafeUtility.SizeOf<T>();

                UnsafeUtility.MemCpy(
                    destination: newArray.GetUnsafePtr()!,
                    source: array.GetUnsafeReadOnlyPtr()!,
                    size: bytesToCopy
                );

                array.Dispose();
                array = newArray;
                accessPtr = (T*)array.GetUnsafePtr();
            }

            public readonly NativeArray<T> InnerNativeArray() =>
                array;

            public void Dispose()
            {
                array.Dispose();
            }
        }
    }
}
