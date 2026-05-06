using DCL.Diagnostics;
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
        bool hasFirstStep;
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
        // Previous-frame parent data per slot. Sim job slerps between this and current
        // parentData each substep so parent rotation distributes smoothly across substeps —
        // prevents flapping when avatar parent rotates a lot per frame at low fps.
        NativeArray<SpringBoneParentData> previousParentData;
        NativeArray<bool> slotActive;
        NativeArray<bool> slotWasActive;
        // Snapshot of post-step world rotation per joint, ping-ponged each substep so render
        // can slerp(prev, curr, alpha) and hide substep boundaries.
        NativeArray<quaternion> prevStepRotations;
        NativeArray<quaternion> currStepRotations;
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
            previousParentData = new NativeArray<SpringBoneParentData>(slotCapacity, Allocator.Persistent);
            slotActive = new NativeArray<bool>(slotCapacity, Allocator.Persistent);
            slotWasActive = new NativeArray<bool>(slotCapacity, Allocator.Persistent);
            prevStepRotations = new NativeArray<quaternion>(totalJoints, Allocator.Persistent);
            currStepRotations = new NativeArray<quaternion>(totalJoints, Allocator.Persistent);

            taa = new TransformAccessArray(managedTransforms);

            // Seed free-list with all fresh slots (descending so lowest pops first)
            for (int i = slotCapacity - 1; i >= previousCapacity; i--)
                freeSlots.Push(i);
        }

        public int RegisterSpring(IReadOnlyList<Transform> jointTransforms, IReadOnlyList<SpringBoneJointConfig> configs, IReadOnlyList<float3> initialTailPositions)
        {
            int jointCount = jointTransforms.Count;

            if (jointCount > MAX_JOINTS_PER_SPRING)
            {
                ReportHub.LogWarning(ReportCategory.AVATAR,
                    $"SpringBone chain has {jointCount} joints, clamping to {MAX_JOINTS_PER_SPRING}. Chain will be truncated.");
                jointCount = MAX_JOINTS_PER_SPRING;
            }

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

            // -1 sentinel marks slot as released. Without it a double-unregister would push the
            // same index onto freeSlots twice and hand the same slot to two callers.
            if (slotJointCounts[slotIndex] < 0) return;

            slotJointCounts[slotIndex] = -1;
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
                    // and seed both world-rotation snapshots so first interpolated push is a no-op slerp.
                    // Also seed prev parent data so first frame's substep parent-interp is a no-op.
                    previousParentData[slot] = parentData[slot];
                    for (int j = 0; j < jointCount; j++)
                    {
                        int idx = baseIndex + j;
                        Transform t = managedTransforms[idx];
                        float3 pos = (float3)t.position;
                        prevTails[idx] = pos;
                        currentTails[idx] = pos;
                        nextTails[idx] = pos;
                        quaternion worldRot = t.rotation;
                        prevStepRotations[idx] = worldRot;
                        currStepRotations[idx] = worldRot;
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
            // Decompose to T/R/S so the simulation job can slerp rotations and lerp position/scale
            // independently each substep. Linear-blending matrix columns instead would skew the
            // upper 3×3 because per-column lerp of two rotations is not a rotation.
            float3 position = localToWorldMatrix.c3.xyz;
            float3 scale = new float3(
                math.length(localToWorldMatrix.c0.xyz),
                math.length(localToWorldMatrix.c1.xyz),
                math.length(localToWorldMatrix.c2.xyz));

            previousParentData[slotIndex] = parentData[slotIndex];
            parentData[slotIndex] = new SpringBoneParentData
            {
                Rotation = rotation,
                Position = position,
                Scale = scale,
                ScaleFactor = scaleFactor,
            };
        }

        public void Simulate(float deltaTime)
        {
            if (slotCapacity == 0) return;

            // Hiccup: avatar teleports a full frame's worth of motion, but physics can only
            // catch up MAX_SUBSTEPS * FIXED_STEP. Trying to integrate the parent jump over
            // partial-frame interp causes massive overshoot (hair whipping). Skip this frame
            // entirely and force a fresh snap on next PrepareSimulation so bones resume from
            // a clean state.
            if (deltaTime > MAX_SUBSTEPS * FIXED_STEP)
            {
                for (int slot = 0; slot < slotCapacity; slot++)
                    if (slotActive[slot]) slotWasActive[slot] = false;
                accumulatedDt = 0f;
                // Treat next frame as a fresh start: skip the trailing slerp-push until a new
                // substep produces a curr rotation, so we don't blend pre-hiccup prev with
                // freshly-snapped curr.
                hasFirstStep = false;
                return;
            }

            // Fixed 60 Hz physics with sub-stepping. Render at any fps interpolates between
            // the last two physics states (slerp) to hide substep boundaries — smooth at
            // 30/45/60/120/244+ fps without aliasing.
            accumulatedDt += deltaTime;

            int totalSubsteps = math.min(MAX_SUBSTEPS, (int)(accumulatedDt / FIXED_STEP));

            for (int i = 0; i < totalSubsteps; i++)
            {
                // prev ← old curr; new step writes into curr
                (prevStepRotations, currStepRotations) = (currStepRotations, prevStepRotations);
                accumulatedDt -= FIXED_STEP;
                // Alpha = fraction of current frame's wall-clock window at which this substep
                // ends. Tracks parent motion continuously across frames regardless of how many
                // substeps fall in any given render frame — avoids parent "rewinding" when a
                // multi-substep frame follows single-substep frames.
                float substepAlpha = deltaTime > 0f
                    ? math.saturate((deltaTime - accumulatedDt) / deltaTime)
                    : 1f;
                SimulateStep(FIXED_STEP, substepAlpha);
                hasFirstStep = true;
            }

            // Drop residual after a stall to avoid spiral-of-death.
            if (totalSubsteps == MAX_SUBSTEPS)
                accumulatedDt = 0f;

            if (!hasFirstStep) return;

            float alpha = math.saturate(accumulatedDt / FIXED_STEP);

            new PushSpringBoneTransformsJob
            {
                PrevRotations = prevStepRotations,
                CurrRotations = currStepRotations,
                SlotActive = slotActive,
                MaxJointsPerSpring = MAX_JOINTS_PER_SPRING,
                Alpha = alpha,
            }.Schedule(taa).Complete();
        }

        void SimulateStep(float deltaTime, float substepAlpha)
        {
            JobHandle pullHandle = new PullSpringBoneTransformsJob
            {
                Transforms = transforms,
                SlotActive = slotActive,
                MaxJointsPerSpring = MAX_JOINTS_PER_SPRING,
            }.Schedule(taa);

            FlipBuffers();

            new SpringBoneSimulationJob
            {
                SlotJointCounts = slotJointCounts,
                SlotActive = slotActive,
                JointConfigs = jointConfigs,
                ParentData = parentData,
                PrevParentData = previousParentData,
                SubstepAlpha = substepAlpha,
                Transforms = transforms,
                PrevTails = prevTails,
                CurrentTails = currentTails,
                NextTails = nextTails,
                CurrStepRotations = currStepRotations,
                DeltaTime = deltaTime,
            }.Schedule(slotCapacity, 8, pullHandle).Complete();
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
            var newPreviousParentData = new NativeArray<SpringBoneParentData>(newCapacity, Allocator.Persistent);
            var newSlotActive = new NativeArray<bool>(newCapacity, Allocator.Persistent);
            var newSlotWasActive = new NativeArray<bool>(newCapacity, Allocator.Persistent);
            var newPrevStepRotations = new NativeArray<quaternion>(newTotalJoints, Allocator.Persistent);
            var newCurrStepRotations = new NativeArray<quaternion>(newTotalJoints, Allocator.Persistent);

            NativeArray<SpringBoneTransformData>.Copy(transforms, newTransforms, oldTotalJoints);
            NativeArray<float3>.Copy(prevTails, newPrevTails, oldTotalJoints);
            NativeArray<float3>.Copy(currentTails, newCurrentTails, oldTotalJoints);
            NativeArray<float3>.Copy(nextTails, newNextTails, oldTotalJoints);
            NativeArray<SpringBoneJointConfig>.Copy(jointConfigs, newJointConfigs, oldTotalJoints);
            NativeArray<int>.Copy(slotJointCounts, newSlotJointCounts, oldCapacity);
            NativeArray<SpringBoneParentData>.Copy(parentData, newParentData, oldCapacity);
            NativeArray<SpringBoneParentData>.Copy(previousParentData, newPreviousParentData, oldCapacity);
            NativeArray<bool>.Copy(slotActive, newSlotActive, oldCapacity);
            NativeArray<bool>.Copy(slotWasActive, newSlotWasActive, oldCapacity);
            NativeArray<quaternion>.Copy(prevStepRotations, newPrevStepRotations, oldTotalJoints);
            NativeArray<quaternion>.Copy(currStepRotations, newCurrStepRotations, oldTotalJoints);

            // Dispose old
            transforms.Dispose();
            prevTails.Dispose();
            currentTails.Dispose();
            nextTails.Dispose();
            jointConfigs.Dispose();
            slotJointCounts.Dispose();
            parentData.Dispose();
            previousParentData.Dispose();
            slotActive.Dispose();
            slotWasActive.Dispose();
            prevStepRotations.Dispose();
            currStepRotations.Dispose();
            taa.Dispose();

            // Assign new
            transforms = newTransforms;
            prevTails = newPrevTails;
            currentTails = newCurrentTails;
            nextTails = newNextTails;
            jointConfigs = newJointConfigs;
            slotJointCounts = newSlotJointCounts;
            parentData = newParentData;
            previousParentData = newPreviousParentData;
            slotActive = newSlotActive;
            slotWasActive = newSlotWasActive;
            prevStepRotations = newPrevStepRotations;
            currStepRotations = newCurrStepRotations;
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
            if (previousParentData.IsCreated) previousParentData.Dispose();
            if (slotActive.IsCreated) slotActive.Dispose();
            if (slotWasActive.IsCreated) slotWasActive.Dispose();
            if (prevStepRotations.IsCreated) prevStepRotations.Dispose();
            if (currStepRotations.IsCreated) currStepRotations.Dispose();
            if (taa.isCreated) taa.Dispose();

            UnityObjectUtils.SafeDestroyGameObject(dummyTransform);
        }
    }
}