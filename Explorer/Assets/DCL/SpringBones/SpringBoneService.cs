using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using Object = UnityEngine.Object;

namespace DCL.SpringBones
{
    /// <summary>
    ///     Implements the spring bones simulation.
    ///     <see cref="RegisterSpring"/> to register a spring to simulate.
    ///     <see cref="UnregisterSpring"/> to stop simulating a spring.
    ///     <see cref="Simulate"/> to tick the simulation.
    /// </summary>
    public class SpringBoneService : IDisposable
    {
        /// <summary>
        ///     Absolute maximum number of bones in a single spring.
        /// </summary>
        public const int MAX_JOINTS_PER_SPRING = 8;

        /// <summary>
        ///     Initial number of available slots. Each slot simulates one spring.
        ///     Can grow, but it's never shrunk.
        /// </summary>
        private const int INITIAL_SLOT_CAPACITY = 32;

        private int slotCapacity;

        private NativeArray<int> slotCountMap; // Number of joints in each slot.
        private NativeArray<BlittableTransform> blittableTransforms;
        private NativeArray<float3> prevTails;
        private NativeArray<float3> currentTails;
        private NativeArray<float3> nextTails;
        private NativeArray<BlittableJointConfig> jointConfigs;
        private NativeArray<BlittableParentData> parentData;

        private TransformAccessArray taa;
        private Transform[] managedTransforms; // A copy of the TAA accessed from the main thread (see Simulate).
        private readonly Transform dummyTransform;

        public SpringBoneService()
        {
            var dummyGo = new GameObject("SpringBoneDummy") { hideFlags = HideFlags.HideAndDontSave };
            dummyTransform = dummyGo.transform;

            slotCapacity = INITIAL_SLOT_CAPACITY;
            AllocateBuffers();
        }

        private void AllocateBuffers()
        {
            // Max joints given initial capacity, but can grow.
            int maxJoints = slotCapacity * MAX_JOINTS_PER_SPRING;

            slotCountMap = new NativeArray<int>(slotCapacity, Allocator.Persistent);
            blittableTransforms = new NativeArray<BlittableTransform>(maxJoints, Allocator.Persistent);
            prevTails = new NativeArray<float3>(maxJoints, Allocator.Persistent);
            currentTails = new NativeArray<float3>(maxJoints, Allocator.Persistent);
            nextTails = new NativeArray<float3>(maxJoints, Allocator.Persistent);
            jointConfigs = new NativeArray<BlittableJointConfig>(maxJoints, Allocator.Persistent);
            parentData = new NativeArray<BlittableParentData>(slotCapacity, Allocator.Persistent);

            var taaTransforms = new Transform[maxJoints];
            for (int i = 0; i < maxJoints; i++) taaTransforms[i] = dummyTransform;
            taa = new TransformAccessArray(taaTransforms);

            managedTransforms = new Transform[maxJoints];
            for (int i = 0; i < maxJoints; i++) managedTransforms[i] = dummyTransform;
        }

        public int RegisterSpring(List<Transform> jointTransforms, List<BlittableJointConfig> configs, List<float3> initialTailPositions)
        {
            int jointCount = jointTransforms.Count;

            int slot = FindEmptySlot();

            slotCountMap[slot] = jointCount;
            int baseIndex = slot * MAX_JOINTS_PER_SPRING;

            for (int i = 0; i < jointCount; i++)
            {
                int j = baseIndex + i;
                jointConfigs[j] = configs[i];
                prevTails[j] = initialTailPositions[i];
                currentTails[j] = initialTailPositions[i];
                nextTails[j] = initialTailPositions[i];
                taa[j] = jointTransforms[i];
                managedTransforms[j] = jointTransforms[i];
            }

            // Fill remaining joints in slot with dummies
            for (int i = jointCount; i < MAX_JOINTS_PER_SPRING; i++)
            {
                int j = baseIndex + i;
                taa[j] = dummyTransform;
                managedTransforms[j] = dummyTransform;
            }

            return slot;
        }

        public void UnregisterSpring(int slotIndex)
        {
            slotCountMap[slotIndex] = 0;
            int baseIndex = slotIndex * MAX_JOINTS_PER_SPRING;

            for (int j = 0; j < MAX_JOINTS_PER_SPRING; j++)
            {
                taa[baseIndex + j] = dummyTransform;
                managedTransforms[baseIndex + j] = dummyTransform;
            }
        }

        public void UpdateParent(int slotIndex, quaternion rotation, float4x4 localToWorldMatrix)
        {
            parentData[slotIndex] = new BlittableParentData
            {
                Rotation = rotation,
                LocalToWorldMatrix = localToWorldMatrix,
            };
        }

        public void Simulate(float deltaTime)
        {
            if (slotCapacity == 0) return;

            RotateBuffers();

            var handle = new PullSpringBoneTransformsJob
            {
                Transforms = blittableTransforms,
            }.ScheduleReadOnly(taa, MAX_JOINTS_PER_SPRING);

            handle = new SpringBoneSimulationJob
            {
                SlotCountMap = slotCountMap,
                JointConfigs = jointConfigs,
                ParentData = parentData,
                Transforms = blittableTransforms,
                PrevTails = prevTails,
                CurrentTails = currentTails,
                NextTails = nextTails,
                DeltaTime = deltaTime,
            }.Schedule(slotCapacity, 1, handle);

            handle.Complete();

            int totalJoints = slotCapacity * MAX_JOINTS_PER_SPRING;
            for (int i = 0; i < totalJoints; i++)
                managedTransforms[i].rotation = blittableTransforms[i].Rotation;
        }

        private void RotateBuffers() =>
            // Rotates buffers so we avoid having to copy data between them
            (prevTails, currentTails, nextTails) = (currentTails, nextTails, prevTails);

        private int FindEmptySlot()
        {
            for (int i = 0; i < slotCapacity; i++) if (slotCountMap[i] == 0) return i;
            return Grow();
        }

        private int Grow()
        {
            int oldSlotCapacity = slotCapacity;
            slotCapacity = oldSlotCapacity * 2;

            int oldTotalJoints = oldSlotCapacity * MAX_JOINTS_PER_SPRING;
            int totalJoints = slotCapacity * MAX_JOINTS_PER_SPRING;

            var oldSlotCountMap = slotCountMap;
            var oldBlittableTransforms = blittableTransforms;
            var oldPrevTails = prevTails;
            var oldCurrentTails = currentTails;
            var oldNextTails = nextTails;
            var oldJointConfigs = jointConfigs;
            var oldParentData = parentData;

            slotCountMap = new NativeArray<int>(slotCapacity, Allocator.Persistent);
            blittableTransforms = new NativeArray<BlittableTransform>(totalJoints, Allocator.Persistent);
            prevTails = new NativeArray<float3>(totalJoints, Allocator.Persistent);
            currentTails = new NativeArray<float3>(totalJoints, Allocator.Persistent);
            nextTails = new NativeArray<float3>(totalJoints, Allocator.Persistent);
            jointConfigs = new NativeArray<BlittableJointConfig>(totalJoints, Allocator.Persistent);
            parentData = new NativeArray<BlittableParentData>(slotCapacity, Allocator.Persistent);

            NativeArray<int>.Copy(oldSlotCountMap, slotCountMap, oldSlotCapacity);
            NativeArray<BlittableTransform>.Copy(oldBlittableTransforms, blittableTransforms, oldTotalJoints);
            NativeArray<float3>.Copy(oldPrevTails, prevTails, oldTotalJoints);
            NativeArray<float3>.Copy(oldCurrentTails, currentTails, oldTotalJoints);
            NativeArray<float3>.Copy(oldNextTails, nextTails, oldTotalJoints);
            NativeArray<BlittableJointConfig>.Copy(oldJointConfigs, jointConfigs, oldTotalJoints);
            NativeArray<BlittableParentData>.Copy(oldParentData, parentData, oldSlotCapacity);

            oldSlotCountMap.Dispose();
            oldBlittableTransforms.Dispose();
            oldPrevTails.Dispose();
            oldCurrentTails.Dispose();
            oldNextTails.Dispose();
            oldJointConfigs.Dispose();
            oldParentData.Dispose();
            taa.Dispose();

            var taaTransforms = new Transform[totalJoints];
            for (int slot = 0; slot < slotCapacity; slot++)
            {
                int baseIndex = slot * MAX_JOINTS_PER_SPRING;
                for (int i = 0; i < MAX_JOINTS_PER_SPRING; i++) taaTransforms[baseIndex + i] = dummyTransform;
            }
            taa = new TransformAccessArray(taaTransforms);

            Array.Resize(ref managedTransforms, totalJoints);
            for (int i = oldTotalJoints; i < totalJoints; i++) managedTransforms[i] = dummyTransform;

            return oldSlotCapacity;
        }

        public void Dispose()
        {
            if (slotCountMap.IsCreated) slotCountMap.Dispose();
            if (blittableTransforms.IsCreated) blittableTransforms.Dispose();
            if (prevTails.IsCreated) prevTails.Dispose();
            if (currentTails.IsCreated) currentTails.Dispose();
            if (nextTails.IsCreated) nextTails.Dispose();
            if (jointConfigs.IsCreated) jointConfigs.Dispose();
            if (parentData.IsCreated) parentData.Dispose();
            if (taa.isCreated) taa.Dispose();

            if (dummyTransform != null) Object.Destroy(dummyTransform.gameObject);
        }
    }
}
