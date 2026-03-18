using System;
using System.Collections.Generic;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Jobs;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    /// <summary>
    ///     Batched pipeline for all remote avatars. Supports dynamic resizing and index recycling.
    ///     Completion is deferred to a later system for maximum parallelism.
    /// </summary>
    internal class RemoteAvatarPipeline : IDisposable
    {
        private readonly int bonesArrayLength;
        private readonly Stack<GlobalJobArrayIndex> releasedIndexes;

        private QuickArray<float4x4> matrixFromAllAvatars;
        private QuickArray<bool> updateAvatar;
        private QuickArray<float4x4> bonesCombined;

        private Transform[][] slotBones;
        private Transform[] slotRoots;

        private TransformAccessArray bonesTransformAccessArray;
        private TransformAccessArray rootsTransformAccessArray;
        private bool structureDirty;

        private JobHandle handle;
        private int avatarIndex;
        private int currentAvatarAmountSupported;

        public BoneMatrixCalculationJob Job;

#if UNITY_INCLUDE_TESTS
        public int MatrixFromAllAvatarsLength => matrixFromAllAvatars.Length;
        public int UpdateAvatarLength => updateAvatar.Length;
        public int CurrentAvatarAmountSupported => currentAvatarAmountSupported;
#endif

        internal RemoteAvatarPipeline(int initialCapacity, int bonesArrayLength, int bonesPerAvatarLength)
        {
            this.bonesArrayLength = bonesArrayLength;

            bonesCombined = new QuickArray<float4x4>(bonesPerAvatarLength);
            Job = new BoneMatrixCalculationJob(bonesArrayLength, bonesPerAvatarLength, bonesCombined.InnerNativeArray());

            matrixFromAllAvatars = new QuickArray<float4x4>(initialCapacity);
            updateAvatar = new QuickArray<bool>(initialCapacity);

            slotBones = new Transform[initialCapacity][];
            slotRoots = new Transform[initialCapacity];

            currentAvatarAmountSupported = initialCapacity;
            releasedIndexes = new Stack<GlobalJobArrayIndex>();
        }

        public void Register(AvatarBase avatarBase, ref AvatarTransformMatrixComponent transformMatrixComponent)
        {
            if (releasedIndexes.Count > 0)
                transformMatrixComponent.IndexInGlobalJobArray = releasedIndexes.Pop();
            else
            {
                transformMatrixComponent.IndexInGlobalJobArray = GlobalJobArrayIndex.ValidUnsafe(avatarIndex);
                avatarIndex++;
            }

            if (transformMatrixComponent.IndexInGlobalJobArray.TryGetValue(out int validIndex) == false)
            {
                ReportHub.LogError(ReportCategory.AVATAR, "Invalid index after direct assignment");
                return;
            }

            slotBones[validIndex] = transformMatrixComponent.bones.Inner;
            slotRoots[validIndex] = avatarBase.transform;
            updateAvatar[validIndex] = true;
            structureDirty = true;

            if (avatarIndex >= currentAvatarAmountSupported - 1)
                ResizeArrays();
        }

        public void Release(ref AvatarTransformMatrixComponent avatarTransformMatrixComponent)
        {
            if (avatarTransformMatrixComponent.IndexInGlobalJobArray.TryGetValue(out int validIndex) == false)
                return;

            updateAvatar[validIndex] = false;

            slotBones[validIndex] = null;
            slotRoots[validIndex] = null;

            releasedIndexes.Push(avatarTransformMatrixComponent.IndexInGlobalJobArray);
            avatarTransformMatrixComponent.IndexInGlobalJobArray = GlobalJobArrayIndex.Unassign();

            structureDirty = true;
        }

        public void Schedule(Transform dummyTransform, int batchCount)
        {
            if (avatarIndex == 0)
                return;

            if (structureDirty)
            {
                handle.Complete();
                RebuildTransformAccessArrays(dummyTransform);
            }

            var boneGatherJob = new BoneGatherJob { BonesCombined = bonesCombined.InnerNativeArray() };
            var boneGatherHandle = boneGatherJob.Schedule(bonesTransformAccessArray);

            var rootGatherJob = new AvatarRootGatherJob { MatrixFromAllAvatars = matrixFromAllAvatars.InnerNativeArray() };
            var rootGatherHandle = rootGatherJob.Schedule(rootsTransformAccessArray);

            var combinedGatherHandle = JobHandle.CombineDependencies(boneGatherHandle, rootGatherHandle);

            Job.AvatarTransform = matrixFromAllAvatars.InnerNativeArray();
            Job.UpdateAvatar = updateAvatar.InnerNativeArray();
            handle = Job.Schedule(avatarIndex, batchCount, combinedGatherHandle);
        }

        public void Complete()
        {
            handle.Complete();
        }

        private void RebuildTransformAccessArrays(Transform dummyTransform)
        {
            if (bonesTransformAccessArray.isCreated) bonesTransformAccessArray.Dispose();
            if (rootsTransformAccessArray.isCreated) rootsTransformAccessArray.Dispose();

            int slotCount = avatarIndex;
            var allBones = new Transform[slotCount * bonesArrayLength];
            var allRoots = new Transform[slotCount];

            for (int slot = 0; slot < slotCount; slot++)
            {
                Transform[] bones = slotBones[slot];
                Transform root = slotRoots[slot];
                allRoots[slot] = root != null ? root : dummyTransform;

                int offset = slot * bonesArrayLength;

                for (int b = 0; b < bonesArrayLength; b++)
                    allBones[offset + b] = bones != null && bones[b] != null ? bones[b] : dummyTransform;
            }

            bonesTransformAccessArray = new TransformAccessArray(allBones);
            rootsTransformAccessArray = new TransformAccessArray(allRoots);
            structureDirty = false;
        }

        private void ResizeArrays()
        {
            int newCapacity = currentAvatarAmountSupported * 2;

            bonesCombined.ReAlloc(newCapacity * bonesArrayLength);
            matrixFromAllAvatars.ReAlloc(newCapacity);
            updateAvatar.ReAlloc(newCapacity);

            Array.Resize(ref slotBones, newCapacity);
            Array.Resize(ref slotRoots, newCapacity);

            Job.Dispose();
            Job = new BoneMatrixCalculationJob(bonesArrayLength, newCapacity * bonesArrayLength, bonesCombined.InnerNativeArray());

            currentAvatarAmountSupported = newCapacity;
            structureDirty = true;
        }

        public void Dispose()
        {
            bonesCombined.Dispose();
            matrixFromAllAvatars.Dispose();
            updateAvatar.Dispose();
            Job.Dispose();

            if (bonesTransformAccessArray.isCreated) bonesTransformAccessArray.Dispose();
            if (rootsTransformAccessArray.isCreated) rootsTransformAccessArray.Dispose();
        }

        /// <summary>
        ///     Unsafe NativeArray wrapper that bypasses runtime bounds checks and supports realloc while preserving data.
        /// </summary>
        private unsafe struct QuickArray<T> : IDisposable where T : unmanaged
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
            ///     Reallocate to exactly newLength, preserving min(old, new) items.
            /// </summary>
            public void ReAlloc(int newLength, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)
            {
                if (!array.IsCreated)
                {
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
