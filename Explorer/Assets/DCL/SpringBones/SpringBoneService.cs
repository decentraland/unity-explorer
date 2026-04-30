using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using Utility;

namespace DCL.SpringBones
{
    public class SpringBoneService : IDisposable
    {
        public const int MAX_JOINTS_PER_SPRING = 8;
        const int INITIAL_SLOT_CAPACITY = 32;
        const float FIXED_STEP = 1f / 60f;
        const int MAX_SUBSTEPS = 4;

        readonly Transform dummyTransform;
        readonly Stack<int> freeSlots = new ();

        float accumulatedDt;
        int slotCapacity;
        Transform[] managedTransforms; // parallel to TAA, for main-thread access
        TransformAccessArray taa;
        NativeArray<SpringBoneTransformData> transforms;
        NativeArray<float3> prevTails;
        NativeArray<float3> currentTails;
        NativeArray<float3> nextTails;
        NativeArray<SpringBoneJointConfig> jointConfigs;
        NativeArray<int> slotJointCounts;
        NativeArray<SpringBoneParentData> parentData;
        NativeArray<bool> slotActive;
        NativeArray<bool> slotWasActive;
        bool disposed;

#if UNITY_INCLUDE_TESTS
        public int SlotCapacity => slotCapacity;
        public int FreeSlotsCount => freeSlots.Count;
        public bool IsDisposed => disposed;
#endif

        public SpringBoneService()
        {
            var go = new GameObject("SpringBoneDummy") { hideFlags = HideFlags.HideAndDontSave };
            dummyTransform = go.transform;

            slotCapacity = INITIAL_SLOT_CAPACITY;
            AllocateArrays(0);
        }

        void AllocateArrays(int previousCapacity)
        {
            int totalJoints = slotCapacity * MAX_JOINTS_PER_SPRING;

            managedTransforms = new Transform[totalJoints];
            for (int i = 0; i < totalJoints; i++) managedTransforms[i] = dummyTransform;

            transforms = new NativeArray<SpringBoneTransformData>(totalJoints, Allocator.Persistent);
            prevTails = new NativeArray<float3>(totalJoints, Allocator.Persistent);
            currentTails = new NativeArray<float3>(totalJoints, Allocator.Persistent);
            nextTails = new NativeArray<float3>(totalJoints, Allocator.Persistent);
            jointConfigs = new NativeArray<SpringBoneJointConfig>(totalJoints, Allocator.Persistent);
            slotJointCounts = new NativeArray<int>(slotCapacity, Allocator.Persistent);
            parentData = new NativeArray<SpringBoneParentData>(slotCapacity, Allocator.Persistent);
            slotActive = new NativeArray<bool>(slotCapacity, Allocator.Persistent);
            slotWasActive = new NativeArray<bool>(slotCapacity, Allocator.Persistent);

            taa = new TransformAccessArray(managedTransforms);

            // Seed free-list with all fresh slots (descending so lowest pops first)
            for (int i = slotCapacity - 1; i >= previousCapacity; i--)
                freeSlots.Push(i);
        }

        public int RegisterSpring(IReadOnlyList<Transform> jointTransforms, IReadOnlyList<SpringBoneJointConfig> configs, IReadOnlyList<float3> initialTailPositions)
        {
            int jointCount = jointTransforms.Count;

            if (freeSlots.Count == 0)
                Grow();

            int slotIndex = freeSlots.Pop();

            slotJointCounts[slotIndex] = jointCount;
            int baseIndex = slotIndex * MAX_JOINTS_PER_SPRING;

            for (int j = 0; j < jointCount; j++)
            {
                int idx = baseIndex + j;
                taa[idx] = jointTransforms[j];
                managedTransforms[idx] = jointTransforms[j];
                jointConfigs[idx] = configs[j];
                prevTails[idx] = initialTailPositions[j];
                currentTails[idx] = initialTailPositions[j];
                nextTails[idx] = initialTailPositions[j];
            }

            // Fill remaining joints in slot with dummies
            for (int j = jointCount; j < MAX_JOINTS_PER_SPRING; j++)
            {
                taa[baseIndex + j] = dummyTransform;
                managedTransforms[baseIndex + j] = dummyTransform;
            }

            return slotIndex;
        }

        public void UnregisterSpring(int slotIndex)
        {
            if (disposed) return;

            slotJointCounts[slotIndex] = 0;
            slotActive[slotIndex] = false;
            slotWasActive[slotIndex] = false;
            int baseIndex = slotIndex * MAX_JOINTS_PER_SPRING;

            for (int j = 0; j < MAX_JOINTS_PER_SPRING; j++)
            {
                taa[baseIndex + j] = dummyTransform;
                managedTransforms[baseIndex + j] = dummyTransform;
            }

            freeSlots.Push(slotIndex);
        }

        public void DeactivateAllSlots()
        {
            for (int i = 0; i < slotCapacity; i++)
                slotActive[i] = false;
        }

        public void SetSlotActive(int slotIndex, bool active) { slotActive[slotIndex] = active; }

        public bool IsSlotActive(int slotIndex) => slotActive[slotIndex];

        public void PrepareSimulation()
        {
            for (int slot = 0; slot < slotCapacity; slot++)
            {
                bool isActive = slotActive[slot];
                bool wasActive = slotWasActive[slot];
                slotWasActive[slot] = isActive;

                if (isActive == wasActive) continue;

                int baseIndex = slot * MAX_JOINTS_PER_SPRING;
                int jointCount = slotJointCounts[slot];

                if (isActive)
                {
                    // Inactive→active: snap tail buffers to current transform position (rest pose)
                    for (int j = 0; j < jointCount; j++)
                    {
                        int idx = baseIndex + j;
                        float3 pos = (float3)managedTransforms[idx].position;
                        prevTails[idx] = pos;
                        currentTails[idx] = pos;
                        nextTails[idx] = pos;
                    }
                }
                else
                {
                    // Active→inactive: reset bones to rest pose rotation
                    for (int j = 0; j < jointCount; j++)
                    {
                        int idx = baseIndex + j;
                        managedTransforms[idx].localRotation = jointConfigs[idx].LocalRotation;
                    }
                }
            }
        }

        public void SetParentData(int slotIndex, quaternion rotation, float4x4 localToWorldMatrix, float scaleFactor)
        {
            parentData[slotIndex] = new SpringBoneParentData
            {
                Rotation = rotation,
                LocalToWorldMatrix = localToWorldMatrix,
                ScaleFactor = scaleFactor,
            };
        }

        public void Simulate(float deltaTime)
        {
            if (slotCapacity == 0) return;

            // Decouple physics from render fps. Step at fixed 60 Hz so authored stiffness/drag
            // produce identical motion regardless of frame rate.
            accumulatedDt += deltaTime;

            int steps = 0;
            while (accumulatedDt >= FIXED_STEP && steps < MAX_SUBSTEPS)
            {
                SimulateStep(FIXED_STEP);
                accumulatedDt -= FIXED_STEP;
                steps++;
            }

            // Drop residual after a stall to avoid spiral-of-death.
            if (steps == MAX_SUBSTEPS)
                accumulatedDt = 0f;
        }

        void SimulateStep(float deltaTime)
        {
            JobHandle pullHandle = new PullSpringBoneTransformsJob
            {
                Transforms = transforms,
                SlotActive = slotActive,
                MaxJointsPerSpring = MAX_JOINTS_PER_SPRING,
            }.Schedule(taa);

            FlipBuffers();

            JobHandle simHandle = new SpringBoneSimulationJob
            {
                SlotJointCounts = slotJointCounts,
                SlotActive = slotActive,
                JointConfigs = jointConfigs,
                ParentData = parentData,
                Transforms = transforms,
                PrevTails = prevTails,
                CurrentTails = currentTails,
                NextTails = nextTails,
                DeltaTime = deltaTime,
            }.Schedule(slotCapacity, 8, pullHandle);

            JobHandle pushHandle = new PushSpringBoneTransformsJob
            {
                Transforms = transforms,
                SlotActive = slotActive,
                MaxJointsPerSpring = MAX_JOINTS_PER_SPRING,
            }.Schedule(taa, simHandle);

            pushHandle.Complete();
        }

        void FlipBuffers()
        {
            // Rotate: prev ← current ← next ← prev
            (prevTails, currentTails, nextTails) = (currentTails, nextTails, prevTails);
        }

        void Grow()
        {
            int oldCapacity = slotCapacity;
            int newCapacity = oldCapacity * 2;
            int oldTotalJoints = oldCapacity * MAX_JOINTS_PER_SPRING;
            int newTotalJoints = newCapacity * MAX_JOINTS_PER_SPRING;

            // Allocate new arrays and copy old data
            var newTransforms = new NativeArray<SpringBoneTransformData>(newTotalJoints, Allocator.Persistent);
            var newPrevTails = new NativeArray<float3>(newTotalJoints, Allocator.Persistent);
            var newCurrentTails = new NativeArray<float3>(newTotalJoints, Allocator.Persistent);
            var newNextTails = new NativeArray<float3>(newTotalJoints, Allocator.Persistent);
            var newJointConfigs = new NativeArray<SpringBoneJointConfig>(newTotalJoints, Allocator.Persistent);
            var newSlotJointCounts = new NativeArray<int>(newCapacity, Allocator.Persistent);
            var newParentData = new NativeArray<SpringBoneParentData>(newCapacity, Allocator.Persistent);
            var newSlotActive = new NativeArray<bool>(newCapacity, Allocator.Persistent);
            var newSlotWasActive = new NativeArray<bool>(newCapacity, Allocator.Persistent);

            NativeArray<SpringBoneTransformData>.Copy(transforms, newTransforms, oldTotalJoints);
            NativeArray<float3>.Copy(prevTails, newPrevTails, oldTotalJoints);
            NativeArray<float3>.Copy(currentTails, newCurrentTails, oldTotalJoints);
            NativeArray<float3>.Copy(nextTails, newNextTails, oldTotalJoints);
            NativeArray<SpringBoneJointConfig>.Copy(jointConfigs, newJointConfigs, oldTotalJoints);
            NativeArray<int>.Copy(slotJointCounts, newSlotJointCounts, oldCapacity);
            NativeArray<SpringBoneParentData>.Copy(parentData, newParentData, oldCapacity);
            NativeArray<bool>.Copy(slotActive, newSlotActive, oldCapacity);
            NativeArray<bool>.Copy(slotWasActive, newSlotWasActive, oldCapacity);

            // Dispose old
            transforms.Dispose();
            prevTails.Dispose();
            currentTails.Dispose();
            nextTails.Dispose();
            jointConfigs.Dispose();
            slotJointCounts.Dispose();
            parentData.Dispose();
            slotActive.Dispose();
            slotWasActive.Dispose();
            taa.Dispose();

            // Assign new
            transforms = newTransforms;
            prevTails = newPrevTails;
            currentTails = newCurrentTails;
            nextTails = newNextTails;
            jointConfigs = newJointConfigs;
            slotJointCounts = newSlotJointCounts;
            parentData = newParentData;
            slotActive = newSlotActive;
            slotWasActive = newSlotWasActive;
            slotCapacity = newCapacity;

            var newManagedTransforms = new Transform[newTotalJoints];
            Array.Copy(managedTransforms, newManagedTransforms, oldTotalJoints);
            for (int i = oldTotalJoints; i < newTotalJoints; i++)
                newManagedTransforms[i] = dummyTransform;
            managedTransforms = newManagedTransforms;

            taa = new TransformAccessArray(newManagedTransforms);

            for (int i = newCapacity - 1; i >= oldCapacity; i--)
                freeSlots.Push(i);
        }

        public void Dispose()
        {
            if (disposed) return;

            disposed = true;

            if (transforms.IsCreated) transforms.Dispose();
            if (prevTails.IsCreated) prevTails.Dispose();
            if (currentTails.IsCreated) currentTails.Dispose();
            if (nextTails.IsCreated) nextTails.Dispose();
            if (jointConfigs.IsCreated) jointConfigs.Dispose();
            if (slotJointCounts.IsCreated) slotJointCounts.Dispose();
            if (parentData.IsCreated) parentData.Dispose();
            if (slotActive.IsCreated) slotActive.Dispose();
            if (slotWasActive.IsCreated) slotWasActive.Dispose();
            if (taa.isCreated) taa.Dispose();

            UnityObjectUtils.SafeDestroyGameObject(dummyTransform);
        }
    }
}