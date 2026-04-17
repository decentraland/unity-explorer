using GLTFast;
using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace DCL.SpringBones
{
    public class SpringBoneService : IDisposable
    {
        public const int MAX_JOINTS_PER_SPRING = 8;
        const int INITIAL_SLOT_CAPACITY = 32;

        readonly Transform dummyTransform;

        int slotCapacity;
        Transform[] managedTransforms; // parallel to TAA, for main-thread access
        SpringBoneJointComponent[] slotRootComponents; // per-slot root authoring component, for live param tweaks
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

        public SpringBoneService()
        {
            var go = new GameObject("SpringBoneDummy") { hideFlags = HideFlags.HideAndDontSave };
            dummyTransform = go.transform;

            slotCapacity = INITIAL_SLOT_CAPACITY;
            AllocateArrays();
        }

        void AllocateArrays()
        {
            int totalJoints = slotCapacity * MAX_JOINTS_PER_SPRING;

            managedTransforms = new Transform[totalJoints];
            for (int i = 0; i < totalJoints; i++) managedTransforms[i] = dummyTransform;

            slotRootComponents = new SpringBoneJointComponent[slotCapacity];

            transforms = new NativeArray<SpringBoneTransformData>(totalJoints, Allocator.Persistent);
            prevTails = new NativeArray<float3>(totalJoints, Allocator.Persistent);
            currentTails = new NativeArray<float3>(totalJoints, Allocator.Persistent);
            nextTails = new NativeArray<float3>(totalJoints, Allocator.Persistent);
            jointConfigs = new NativeArray<SpringBoneJointConfig>(totalJoints, Allocator.Persistent);
            slotJointCounts = new NativeArray<int>(slotCapacity, Allocator.Persistent);
            parentData = new NativeArray<SpringBoneParentData>(slotCapacity, Allocator.Persistent);
            slotActive = new NativeArray<bool>(slotCapacity, Allocator.Persistent);
            slotWasActive = new NativeArray<bool>(slotCapacity, Allocator.Persistent);

            // Build TAA filled with dummies
            var taaTransforms = new Transform[totalJoints];
            for (int i = 0; i < totalJoints; i++) taaTransforms[i] = dummyTransform;
            taa = new TransformAccessArray(taaTransforms);
        }

        public int RegisterSpring(Transform[] jointTransforms, SpringBoneJointConfig[] configs, float3[] initialTailPositions, SpringBoneJointComponent rootComponent)
        {
            int jointCount = jointTransforms.Length;

            int slotIndex = FindEmptySlot();
            if (slotIndex < 0)
            {
                Grow();
                slotIndex = FindEmptySlot();
            }

            slotJointCounts[slotIndex] = jointCount;
            slotRootComponents[slotIndex] = rootComponent;
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
                taa[baseIndex + j] = dummyTransform;

            return slotIndex;
        }

        public void UnregisterSpring(int slotIndex)
        {
            slotJointCounts[slotIndex] = 0;
            slotRootComponents[slotIndex] = null;
            int baseIndex = slotIndex * MAX_JOINTS_PER_SPRING;

            for (int j = 0; j < MAX_JOINTS_PER_SPRING; j++)
            {
                taa[baseIndex + j] = dummyTransform;
                managedTransforms[baseIndex + j] = dummyTransform;
            }
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

        public void SetParentData(int slotIndex, quaternion rotation, float4x4 localToWorldMatrix)
        {
            parentData[slotIndex] = new SpringBoneParentData
            {
                Rotation = rotation,
                LocalToWorldMatrix = localToWorldMatrix,
            };
        }

        // Pull mutable params (Stiffness/Drag/GravityDir/GravityPower) from the root authoring
        // component into jointConfigs for every active slot. Lets designers tweak values at
        // runtime via the Inspector and see changes applied on the next simulate tick.
        void RefreshActiveJointConfigsFromComponents()
        {
            for (int slot = 0; slot < slotCapacity; slot++)
            {
                if (!slotActive[slot]) continue;

                SpringBoneJointComponent root = slotRootComponents[slot];
                if (root == null) continue;

                int baseIndex = slot * MAX_JOINTS_PER_SPRING;
                int jointCount = slotJointCounts[slot];

                float stiffness = root.Stiffness;
                float drag = root.Drag;
                float3 gravityDir = root.GravityDir;
                float gravityPower = root.GravityPower;

                for (int j = 0; j < jointCount; j++)
                {
                    int idx = baseIndex + j;
                    SpringBoneJointConfig cfg = jointConfigs[idx];
                    cfg.Stiffness = stiffness;
                    cfg.Drag = drag;
                    cfg.GravityDir = gravityDir;
                    cfg.GravityPower = gravityPower;
                    jointConfigs[idx] = cfg;
                }
            }
        }

        public void Simulate(float deltaTime)
        {
            if (slotCapacity == 0) return;

            RefreshActiveJointConfigsFromComponents();

            // 1. Pull transforms on main thread — only active slots
            for (int slot = 0; slot < slotCapacity; slot++)
            {
                if (!slotActive[slot]) continue;

                int baseIndex = slot * MAX_JOINTS_PER_SPRING;
                int jointCount = slotJointCounts[slot];

                for (int j = 0; j < jointCount; j++)
                {
                    int idx = baseIndex + j;
                    transforms[idx] = SpringBoneTransformData.FromTransform(managedTransforms[idx]);
                }
            }

            // 2. Flip tail buffers: prev ← current ← next ← prev
            FlipBuffers();

            // 3. Run simulation (still as a job for Burst)
            new SpringBoneSimulationJob
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
            }.Schedule(slotCapacity, 1).Complete();

            // 4. Push rotations back on main thread — only active slots
            for (int slot = 0; slot < slotCapacity; slot++)
            {
                if (!slotActive[slot]) continue;

                int baseIndex = slot * MAX_JOINTS_PER_SPRING;
                int jointCount = slotJointCounts[slot];

                for (int j = 0; j < jointCount; j++)
                {
                    int idx = baseIndex + j;
                    managedTransforms[idx].rotation = transforms[idx].Rotation;
                }
            }
        }

        void FlipBuffers()
        {
            // Rotate: prev ← current ← next ← prev
            (prevTails, currentTails, nextTails) = (currentTails, nextTails, prevTails);
        }

        int FindEmptySlot()
        {
            for (int i = 0; i < slotCapacity; i++)
                if (slotJointCounts[i] == 0) return i;
            return -1;
        }

        void Grow()
        {
            int newCapacity = slotCapacity * 2;
            int oldTotalJoints = slotCapacity * MAX_JOINTS_PER_SPRING;
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
            NativeArray<int>.Copy(slotJointCounts, newSlotJointCounts, slotCapacity);
            NativeArray<SpringBoneParentData>.Copy(parentData, newParentData, slotCapacity);
            NativeArray<bool>.Copy(slotActive, newSlotActive, slotCapacity);
            NativeArray<bool>.Copy(slotWasActive, newSlotWasActive, slotCapacity);

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

            // Resize managed transforms array (was missing — caused IndexOutOfRangeException)
            var newManagedTransforms = new Transform[newTotalJoints];
            Array.Copy(managedTransforms, newManagedTransforms, oldTotalJoints);
            for (int i = oldTotalJoints; i < newTotalJoints; i++)
                newManagedTransforms[i] = dummyTransform;
            managedTransforms = newManagedTransforms;

            var newSlotRootComponents = new SpringBoneJointComponent[newCapacity];
            Array.Copy(slotRootComponents, newSlotRootComponents, slotRootComponents.Length);
            slotRootComponents = newSlotRootComponents;

            // Rebuild TAA
            var taaTransforms = new Transform[newTotalJoints];

            for (int slot = 0; slot < newCapacity; slot++)
            {
                int baseIdx = slot * MAX_JOINTS_PER_SPRING;

                // We can't recover the original Transform references from the old TAA (it's disposed).
                // For existing slots, the registration system will re-set TAA entries on next re-registration.
                // For now, fill everything with dummies. Active slots will be corrected next frame.
                for (int j = 0; j < MAX_JOINTS_PER_SPRING; j++)
                    taaTransforms[baseIdx + j] = dummyTransform;
            }

            taa = new TransformAccessArray(taaTransforms);
        }

        public void Dispose()
        {
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

            if (dummyTransform != null)
                UnityEngine.Object.Destroy(dummyTransform.gameObject);
        }
    }
}
