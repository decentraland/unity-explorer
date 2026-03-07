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
    public class AvatarTransformMatrixJobWrapper : IDisposable
    {
        private bool disposed;

        // Each task processes one full avatar (62 bone multiplies). Small batch count keeps
        // worker utilisation high without excessive scheduling overhead.
        private const int BONE_MATRIX_BATCH_COUNT = 4;

        internal const int AVATAR_ARRAY_SIZE = 100;
        private const int BONES_ARRAY_LENGTH = ComputeShaderConstants.BONE_COUNT;
        private const int BONES_PER_AVATAR_LENGTH = AVATAR_ARRAY_SIZE * BONES_ARRAY_LENGTH;

        private QuickArray<float4x4> matrixFromAllAvatars;
        private QuickArray<bool> updateAvatar;
        private QuickArray<float4x4> bonesCombined;
        public BoneMatrixCalculationJob job;

        // Per-slot Transform references used to rebuild the TransformAccessArrays when avatars are added or removed.
        // Null entries indicate released/unassigned slots and are replaced with dummyTransform during rebuild.
        private Transform[][] slotBones;
        private Transform[] slotRoots;

        // TransformAccessArrays for the Burst-compiled gather jobs.
        // Layout: bonesTransformAccessArray[slot * BONES_ARRAY_LENGTH + boneIdx] = bone transform for that slot.
        // rootsTransformAccessArray[slot] = avatar root transform for that slot.
        private TransformAccessArray bonesTransformAccessArray;
        private TransformAccessArray rootsTransformAccessArray;

        // Set to true whenever the slot layout changes (register or release). Triggers a TAA rebuild before the next gather.
        private bool structureDirty;

        // Placeholder transform used in the TAA for released or unassigned slots so the gather jobs always
        // read valid (though unused) transforms.
        private readonly Transform dummyTransform;

        private JobHandle handle;
        private readonly Stack<GlobalJobArrayIndex> releasedIndexes;

        private int avatarIndex;
        private int currentAvatarAmountSupported;

#if UNITY_INCLUDE_TESTS
        public int MatrixFromAllAvatarsLength => matrixFromAllAvatars.Length;
        public int UpdateAvatarLength => updateAvatar.Length;
        public int CurrentAvatarAmountSupported => currentAvatarAmountSupported;
#endif

        public AvatarTransformMatrixJobWrapper()
        {
            var dummyGO = new GameObject("AvatarTransformMatrixDummy") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(dummyGO);
            dummyTransform = dummyGO.transform;

            bonesCombined = new QuickArray<float4x4>(BONES_PER_AVATAR_LENGTH);
            job = new BoneMatrixCalculationJob(BONES_ARRAY_LENGTH, BONES_PER_AVATAR_LENGTH, bonesCombined.InnerNativeArray());

            matrixFromAllAvatars = new QuickArray<float4x4>(AVATAR_ARRAY_SIZE);
            updateAvatar = new QuickArray<bool>(AVATAR_ARRAY_SIZE);

            slotBones = new Transform[AVATAR_ARRAY_SIZE][];
            slotRoots = new Transform[AVATAR_ARRAY_SIZE];

            currentAvatarAmountSupported = AVATAR_ARRAY_SIZE;
            releasedIndexes = new Stack<GlobalJobArrayIndex>();
        }

        /// <summary>
        ///     Schedules the bone gather jobs followed by the matrix calculation job.
        ///     Gather jobs read Transform data on worker threads via TransformAccessArray so
        ///     the main thread no longer pays the cost of iterating every bone per avatar.
        /// </summary>
        public void ScheduleBoneMatrixCalculation()
        {
            if (avatarIndex == 0)
                return;

            if (structureDirty)
            {
                // Must complete any in-flight job before disposing and rebuilding the TAAs.
                handle.Complete();
                RebuildTransformAccessArrays();
            }

            var boneGatherJob = new BoneGatherJob { BonesCombined = bonesCombined.InnerNativeArray() };
            var boneGatherHandle = boneGatherJob.Schedule(bonesTransformAccessArray);

            var rootGatherJob = new AvatarRootGatherJob { MatrixFromAllAvatars = matrixFromAllAvatars.InnerNativeArray() };
            var rootGatherHandle = rootGatherJob.Schedule(rootsTransformAccessArray);

            var combinedGatherHandle = JobHandle.CombineDependencies(boneGatherHandle, rootGatherHandle);

            job.AvatarTransform = matrixFromAllAvatars.InnerNativeArray();
            job.UpdateAvatar = updateAvatar.InnerNativeArray();
            handle = job.Schedule(avatarIndex, BONE_MATRIX_BATCH_COUNT, combinedGatherHandle);
        }

        public void CompleteBoneMatrixCalculations()
        {
            handle.Complete();
        }

        /// <summary>
        ///     Registers an avatar for bone matrix calculation on its first call.
        ///     Subsequent calls for already-registered avatars are no-ops; per-frame work is handled by the gather jobs.
        /// </summary>
        public void RegisterAvatar(AvatarBase avatarBase, ref AvatarTransformMatrixComponent transformMatrixComponent)
        {
            if (transformMatrixComponent.IndexInGlobalJobArray.IsValid())
                return;

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

        private void RebuildTransformAccessArrays()
        {
            if (bonesTransformAccessArray.isCreated) bonesTransformAccessArray.Dispose();
            if (rootsTransformAccessArray.isCreated) rootsTransformAccessArray.Dispose();

            int slotCount = avatarIndex;
            var allBones = new Transform[slotCount * BONES_ARRAY_LENGTH];
            var allRoots = new Transform[slotCount];

            for (int slot = 0; slot < slotCount; slot++)
            {
                Transform[] bones = slotBones[slot];
                Transform root = slotRoots[slot];
                allRoots[slot] = root != null ? root : dummyTransform;

                int offset = slot * BONES_ARRAY_LENGTH;

                for (int b = 0; b < BONES_ARRAY_LENGTH; b++)
                    allBones[offset + b] = bones != null && bones[b] != null ? bones[b] : dummyTransform;
            }

            bonesTransformAccessArray = new TransformAccessArray(allBones);
            rootsTransformAccessArray = new TransformAccessArray(allRoots);
            structureDirty = false;
        }

        private void ResizeArrays()
        {
            int newCapacity = currentAvatarAmountSupported * 2;

            bonesCombined.ReAlloc(newCapacity * BONES_ARRAY_LENGTH);
            matrixFromAllAvatars.ReAlloc(newCapacity);
            updateAvatar.ReAlloc(newCapacity);

            Array.Resize(ref slotBones, newCapacity);
            Array.Resize(ref slotRoots, newCapacity);

            job.Dispose();
            job = new BoneMatrixCalculationJob(BONES_ARRAY_LENGTH, newCapacity * BONES_ARRAY_LENGTH, bonesCombined.InnerNativeArray());

            currentAvatarAmountSupported = newCapacity;
            structureDirty = true;
        }

        public void Dispose()
        {
            handle.Complete();
            bonesCombined.Dispose();
            updateAvatar.Dispose();
            matrixFromAllAvatars.Dispose();
            job.Dispose();

            if (bonesTransformAccessArray.isCreated) bonesTransformAccessArray.Dispose();
            if (rootsTransformAccessArray.isCreated) rootsTransformAccessArray.Dispose();

            if (dummyTransform != null)
                UnityEngine.Object.Destroy(dummyTransform.gameObject);

            disposed = true;
        }

        public void ReleaseAvatar(ref AvatarTransformMatrixComponent avatarTransformMatrixComponent)
        {
            if (disposed) return;

            if (avatarTransformMatrixComponent.IndexInGlobalJobArray.TryGetValue(out int validIndex) == false)
                return;

            updateAvatar[validIndex] = false;

            // Null out Transform references so the next TAA rebuild uses dummyTransform for this slot,
            // preventing access to potentially-destroyed transforms on worker threads.
            slotBones[validIndex] = null;
            slotRoots[validIndex] = null;

            releasedIndexes.Push(avatarTransformMatrixComponent.IndexInGlobalJobArray);
            avatarTransformMatrixComponent.IndexInGlobalJobArray = GlobalJobArrayIndex.Unassign();

            structureDirty = true;
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