using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Loading.Assets;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.SpringBones
{
    [LogCategory(ReportCategory.AVATAR)]
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(AvatarInstantiatorSystem))]
    public partial class SpringBoneRegistrationSystem : BaseUnityLoopSystem
    {
        private readonly SpringBoneService springBoneService;

        public SpringBoneRegistrationSystem(World world, SpringBoneService springBoneService) : base(world)
        {
            this.springBoneService = springBoneService;
        }

        protected override void Update(float t)
        {
            RegisterNewQuery(World);
            ReRegisterOnChangeQuery(World);
            CleanUpOnDeleteQuery(World);
        }

        protected override void OnDispose() =>
            CleanUpOnDisposeQuery(World);

        [Query]
        [None(typeof(SpringBoneRegistrationComponent), typeof(DeleteEntityIntention))]
        private void RegisterNew(in Entity entity,
            ref AvatarShapeComponent avatarShapeComponent,
            AvatarBase avatarBase,
            ref AvatarTransformMatrixComponent transformMatrixComponent)
        {
            var slotIndices = ListPool<int>.Get();
            var syncPairs = ListPool<(Transform, Transform)>.Get();

            RegisterSprings(avatarShapeComponent.InstantiatedWearables, avatarBase,
                ref transformMatrixComponent, slotIndices, syncPairs);

            World.Add(entity, new SpringBoneRegistrationComponent
            {
                SlotIndices = slotIndices,
                AvatarVersion = avatarShapeComponent.InstantiationVersion,
                SyncPairs = syncPairs,
            });
        }

        [Query]
        [All(typeof(AvatarShapeComponent), typeof(AvatarBase), typeof(AvatarTransformMatrixComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void ReRegisterOnChange(ref AvatarShapeComponent avatarShapeComponent,
            AvatarBase avatarBase,
            ref SpringBoneRegistrationComponent registration,
            ref AvatarTransformMatrixComponent transformMatrixComponent)
        {
            if (avatarShapeComponent.InstantiationVersion == registration.AvatarVersion) return;

            registration.AvatarVersion = avatarShapeComponent.InstantiationVersion;

            UnregisterSlots(registration.SlotIndices);
            registration.SyncPairs.Clear();

            RegisterSprings(avatarShapeComponent.InstantiatedWearables, avatarBase,
                ref transformMatrixComponent, registration.SlotIndices, registration.SyncPairs);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void CleanUpOnDelete(ref SpringBoneRegistrationComponent registration) =>
            CleanUp(ref registration);

        [Query]
        private void CleanUpOnDispose(ref SpringBoneRegistrationComponent registration) =>
            CleanUp(ref registration);

        private void RegisterSprings(IList<CachedAttachment> wearables, AvatarBase avatarBase,
            ref AvatarTransformMatrixComponent transformMatrixComponent,
            List<int> slotIndices, List<(Transform, Transform)> syncPairs)
        {
            Transform[] skeleton = avatarBase.AvatarSkinnedMeshRenderer.bones;

            using var springBoneTransformsScope = ListPool<Transform>.Get(out var springBoneTransforms);

            foreach (CachedAttachment wearable in wearables)
            {
                using var chainJointsScope = ListPool<Transform>.Get(out var chainJoints);
                using var chainConfigsScope = ListPool<SpringBoneJointConfig>.Get(out var chainConfigs);
                using var chainTailsScope = ListPool<Unity.Mathematics.float3>.Get(out var chainTails);

                foreach (SpringBoneData springBone in wearable.SpringBones)
                {
                    springBoneTransforms.Add(springBone.ManagedTransform);
                    springBone.ManagedTransform.localRotation = springBone.InitialLocalRotation;

                    if (springBone.IsRoot)
                    {
                        // Flush any pending chain from a previous root in this wearable
                        if (chainJoints.Count > 0)
                        {
                            int slotIndex = RegisterChain(chainJoints, chainConfigs, chainTails);
                            slotIndices.Add(slotIndex);
                            chainJoints.Clear();
                            chainConfigs.Clear();
                            chainTails.Clear();
                        }

                        // Sync wearable parent to avatar skeleton bone
                        Transform wearableParent = springBone.ManagedTransform.parent;
                        Transform avatarParent = skeleton[springBone.AvatarSkeletonParentBoneIndex];
                        wearableParent.SetPositionAndRotation(avatarParent.position, avatarParent.rotation);

                        Vector3 parentOfWearableLossyScale = wearableParent.parent != null ? wearableParent.parent.lossyScale : Vector3.one;
                        Vector3 avatarLossyScale = avatarParent.lossyScale;
                        wearableParent.localScale = new Vector3(
                            avatarLossyScale.x / parentOfWearableLossyScale.x,
                            avatarLossyScale.y / parentOfWearableLossyScale.y,
                            avatarLossyScale.z / parentOfWearableLossyScale.z);
                        syncPairs.Add((wearableParent, avatarParent));
                    }

                    chainJoints.Add(springBone.ManagedTransform);
                    chainConfigs.Add(BuildJointConfig(springBone));
                }

                // Register the last chain of this wearable
                if (chainJoints.Count > 0)
                {
                    // Compute tail positions now that parent sync is done
                    ComputeInitialTailPositions(chainJoints, chainTails);

                    int lastSlot = RegisterChain(chainJoints, chainConfigs, chainTails);
                    slotIndices.Add(lastSlot);
                }
            }

            if (springBoneTransforms.Count > 0)
            {
                transformMatrixComponent.bones.Append(springBoneTransforms);
                transformMatrixComponent.IndexInGlobalJobArray = GlobalJobArrayIndex.Uninitialized();
            }
        }

        private int RegisterChain(List<Transform> joints, List<SpringBoneJointConfig> configs, List<float3> tails)
        {
            // Compute tail positions and bone axis/length for the configs
            for (int j = 0; j < joints.Count; j++)
            {
                var config = configs[j];
                Transform tail = (j + 1 < joints.Count) ? joints[j + 1] : null;

                if (tail != null)
                {
                    float3 localPos = tail.localPosition;
                    float3 scale = tail.lossyScale;
                    float3 scaledPos = localPos * scale;
                    float len = math.length(scaledPos);
                    config.BoneAxis = len > 0.0001f ? scaledPos / len : new float3(0, 1, 0);
                    config.Length = len;
                }
                else
                {
                    config.BoneAxis = new float3(0, 1, 0);
                    config.Length = 0.1f;
                }

                configs[j] = config;
            }

            ComputeInitialTailPositions(joints, tails);

            return springBoneService.RegisterSpring(
                joints.ToArray(),
                configs.ToArray(),
                tails.ToArray());
        }

        static void ComputeInitialTailPositions(List<Transform> joints, List<float3> tails)
        {
            tails.Clear();

            for (int j = 0; j < joints.Count; j++)
            {
                float3 tailPos = j + 1 < joints.Count
                    ? joints[j + 1].position
                    : joints[j].position;
                tails.Add(tailPos);
            }
        }

        static SpringBoneJointConfig BuildJointConfig(SpringBoneData data)
        {
            return new SpringBoneJointConfig
            {
                Stiffness = data.Stiffness,
                Drag = data.Drag,
                GravityDir = data.GravityDir,
                GravityPower = data.GravityPower,
                LocalRotation = data.InitialLocalRotation,
                // BoneAxis and Length are computed later in RegisterChain
            };
        }

        private void UnregisterSlots(List<int> slotIndices)
        {
            foreach (int slotIndex in slotIndices)
                springBoneService.UnregisterSpring(slotIndex);

            slotIndices.Clear();
        }

        private void CleanUp(ref SpringBoneRegistrationComponent registration)
        {
            if (registration.SlotIndices != null)
            {
                UnregisterSlots(registration.SlotIndices);
                ListPool<int>.Release(registration.SlotIndices);
                registration.SlotIndices = null;
            }

            if (registration.SyncPairs != null)
            {
                ListPool<(Transform, Transform)>.Release(registration.SyncPairs);
                registration.SyncPairs = null;
            }
        }
    }
}
