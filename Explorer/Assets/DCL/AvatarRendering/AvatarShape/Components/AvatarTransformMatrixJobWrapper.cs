using System;
using System.Collections.Generic;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Jobs;
using UnityEngine.Profiling;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    public unsafe class AvatarTransformMatrixJobWrapper : IDisposable
    {
        private const int INNER_LOOP_BATCH_COUNT = 128; // Each iteration is lightweight. Reduces overhead from frequent job switching.
        private const int WORLD_MATRIX_CALCULATION_STRATEGY_LIMIT = 128; // There is no gain in performance to multithreading for less amount of avatars

        internal const int AVATAR_ARRAY_SIZE = 100;
        private static readonly int BONES_ARRAY_LENGTH = ComputeShaderConstants.BONE_COUNT;
        private static readonly int BONES_PER_AVATAR_LENGTH = AVATAR_ARRAY_SIZE * BONES_ARRAY_LENGTH;

        private QuickArray<Matrix4x4> matrixFromAllAvatars;
        private QuickArray<bool> updateAvatar;

        private TransformAccessArray copyBufferPerAvatar;

        private QuickArray<Matrix4x4> bonesCombined;
        public BoneMatrixCalculationJob job;

        private JobHandle handle;

        private readonly Stack<int> releasedIndexes;

        private int avatarIndex;
        private int nextResizeValue;
        internal int currentAvatarAmountSupported;

#if UNITY_INCLUDE_TESTS
        public int MatrixFromAllAvatarsLength => matrixFromAllAvatars.Length;
        public int UpdateAvatarLength => updateAvatar.Length;
#endif

        public AvatarTransformMatrixJobWrapper()
        {
            bonesCombined = new QuickArray<Matrix4x4>(BONES_PER_AVATAR_LENGTH);
            copyBufferPerAvatar = new TransformAccessArray(BONES_ARRAY_LENGTH);

            job = new BoneMatrixCalculationJob(BONES_ARRAY_LENGTH, BONES_PER_AVATAR_LENGTH, bonesCombined.InnerNativeArray());

            matrixFromAllAvatars = new QuickArray<Matrix4x4>(AVATAR_ARRAY_SIZE);
            updateAvatar = new QuickArray<bool>(AVATAR_ARRAY_SIZE);

            currentAvatarAmountSupported = AVATAR_ARRAY_SIZE;

            nextResizeValue = 2;
            releasedIndexes = new Stack<int>();
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
                var worldMatrixJob = new WorldMatrixCalculationJob(bonesCombined.InnerNativeArray(), globalIndexOffset);
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

        private void ResizeArrays()
        {
            bonesCombined.ReAlloc(BONES_PER_AVATAR_LENGTH * nextResizeValue);
            matrixFromAllAvatars.ReAlloc(AVATAR_ARRAY_SIZE * nextResizeValue);
            updateAvatar.ReAlloc(AVATAR_ARRAY_SIZE * nextResizeValue);

            job.BonesMatricesResult.Dispose();
            job = new BoneMatrixCalculationJob(BONES_ARRAY_LENGTH, BONES_PER_AVATAR_LENGTH * nextResizeValue, bonesCombined.InnerNativeArray());

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
            updateAvatar[avatarTransformMatrixComponent.IndexInGlobalJobArray] = false;
            releasedIndexes.Push(avatarTransformMatrixComponent.IndexInGlobalJobArray);

            avatarTransformMatrixComponent.IndexInGlobalJobArray = -1;
        }

        /// <summary>
        /// Implementation operates on NativeArray and mitigates runtime checks for elements access. Supports realloc
        /// </summary>
        private struct QuickArray<T> : IDisposable where T: unmanaged
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
