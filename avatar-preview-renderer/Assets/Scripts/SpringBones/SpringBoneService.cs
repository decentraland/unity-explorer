using System;
using Unity.Mathematics;
using UnityEngine;

namespace SpringBones
{
    public class SpringBoneService : IDisposable
    {
        public const int MAX_JOINTS_PER_SPRING = 8;
        const int INITIAL_SLOT_CAPACITY = 32;
        const float FIXED_STEP = 1f / 60f;
        const int MAX_SUBSTEPS = 4;

        int slotCapacity;
        Transform[] managedTransforms;
        SpringBoneTransformData[] transforms;
        float3[] prevTails;
        float3[] currentTails;
        float3[] nextTails;
        SpringBoneJointConfig[] jointConfigs;
        int[] slotJointCounts;
        SpringBoneParentData[] parentData;
        float accumulatedDt;

        public SpringBoneService()
        {
            slotCapacity = INITIAL_SLOT_CAPACITY;
            AllocateArrays();
        }

        void AllocateArrays()
        {
            int totalJoints = slotCapacity * MAX_JOINTS_PER_SPRING;

            managedTransforms = new Transform[totalJoints];
            transforms = new SpringBoneTransformData[totalJoints];
            prevTails = new float3[totalJoints];
            currentTails = new float3[totalJoints];
            nextTails = new float3[totalJoints];
            jointConfigs = new SpringBoneJointConfig[totalJoints];
            slotJointCounts = new int[slotCapacity];
            parentData = new SpringBoneParentData[slotCapacity];
        }

        public int RegisterSpring(Transform[] jointTransforms, SpringBoneJointConfig[] configs, float3[] initialTailPositions)
        {
            int jointCount = jointTransforms.Length;

            int slotIndex = FindEmptySlot();
            if (slotIndex < 0)
            {
                Grow();
                slotIndex = FindEmptySlot();
            }

            slotJointCounts[slotIndex] = jointCount;
            int baseIndex = slotIndex * MAX_JOINTS_PER_SPRING;

            for (int j = 0; j < jointCount; j++)
            {
                int idx = baseIndex + j;
                managedTransforms[idx] = jointTransforms[j];
                jointConfigs[idx] = configs[j];
                prevTails[idx] = initialTailPositions[j];
                currentTails[idx] = initialTailPositions[j];
                nextTails[idx] = initialTailPositions[j];
            }

            return slotIndex;
        }

        public void UnregisterSpring(int slotIndex)
        {
            slotJointCounts[slotIndex] = 0;
            int baseIndex = slotIndex * MAX_JOINTS_PER_SPRING;

            for (int j = 0; j < MAX_JOINTS_PER_SPRING; j++)
                managedTransforms[baseIndex + j] = null;
        }

        public void SetParentData(int slotIndex, quaternion rotation, float4x4 localToWorldMatrix)
        {
            parentData[slotIndex] = new SpringBoneParentData
            {
                Rotation = rotation,
                LocalToWorldMatrix = localToWorldMatrix,
            };
        }

        public void Simulate(float deltaTime)
        {
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
            for (int slot = 0; slot < slotCapacity; slot++)
            {
                int jointCount = slotJointCounts[slot];
                if (jointCount == 0) continue;

                int baseIndex = slot * MAX_JOINTS_PER_SPRING;
                for (int j = 0; j < jointCount; j++)
                {
                    int idx = baseIndex + j;
                    transforms[idx] = SpringBoneTransformData.FromTransform(managedTransforms[idx]);
                }
            }

            // Rotate ring buffer: prev gets old current, current gets old next, next becomes scratch (old prev).
            (prevTails, currentTails, nextTails) = (currentTails, nextTails, prevTails);

            for (int slot = 0; slot < slotCapacity; slot++)
            {
                int jointCount = slotJointCounts[slot];
                if (jointCount == 0) continue;

                SpringBoneSimulator.SimulateSlot(
                    slot, jointCount, jointConfigs, parentData[slot],
                    transforms, prevTails, currentTails, nextTails, deltaTime);
            }

            for (int slot = 0; slot < slotCapacity; slot++)
            {
                int jointCount = slotJointCounts[slot];
                if (jointCount == 0) continue;

                int baseIndex = slot * MAX_JOINTS_PER_SPRING;
                for (int j = 0; j < jointCount; j++)
                {
                    int idx = baseIndex + j;
                    managedTransforms[idx].rotation = transforms[idx].Rotation;
                }
            }
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

            Array.Resize(ref managedTransforms, newTotalJoints);
            Array.Resize(ref transforms, newTotalJoints);
            Array.Resize(ref prevTails, newTotalJoints);
            Array.Resize(ref currentTails, newTotalJoints);
            Array.Resize(ref nextTails, newTotalJoints);
            Array.Resize(ref jointConfigs, newTotalJoints);
            Array.Resize(ref slotJointCounts, newCapacity);
            Array.Resize(ref parentData, newCapacity);

            slotCapacity = newCapacity;
        }

        public void Dispose()
        {
            managedTransforms = null;
            transforms = null;
            prevTails = null;
            currentTails = null;
            nextTails = null;
            jointConfigs = null;
            slotJointCounts = null;
            parentData = null;
            accumulatedDt = 0f;
        }
    }
}
