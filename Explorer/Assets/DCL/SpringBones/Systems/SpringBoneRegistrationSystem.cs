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
            var slots = ListPool<SpringBoneSlot>.Get();

            RegisterSprings(avatarShapeComponent.InstantiatedWearables, avatarBase, ref transformMatrixComponent, slots);

            World.Add(entity, new SpringBoneRegistrationComponent
            {
                Slots = slots,
                AvatarVersion = avatarShapeComponent.InstantiationVersion,
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

            // The avatar was rebuilt: Transform refs captured in our slots may now point at recycled
            // or destroyed GameObjects (attachment cache can evict between release and re-instantiation),
            // so always unregister and register fresh. Visual pop is acceptable on rebuild.
            registration.AvatarVersion = avatarShapeComponent.InstantiationVersion;

            UnregisterSlots(registration.Slots);

            RegisterSprings(avatarShapeComponent.InstantiatedWearables, avatarBase,
                ref transformMatrixComponent, registration.Slots);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void CleanUpOnDelete(ref SpringBoneRegistrationComponent registration) =>
            CleanUp(ref registration);

        [Query]
        private void CleanUpOnDispose(ref SpringBoneRegistrationComponent registration) =>
            CleanUp(ref registration);

        private void RegisterSprings(IList<CachedAttachment> wearables, AvatarBase avatarBase,
            ref AvatarTransformMatrixComponent transformMatrixComponent, List<SpringBoneSlot> slots)
        {
            Transform[] skeleton = avatarBase.AvatarSkinnedMeshRenderer.bones;

            using var springBoneTransformsScope = ListPool<Transform>.Get(out var springBoneTransforms);

            foreach (CachedAttachment wearable in wearables)
            {
                using var chainJointsScope = ListPool<Transform>.Get(out var chainJoints);
                using var chainConfigsScope = ListPool<SpringBoneJointConfig>.Get(out var chainConfigs);
                using var chainTailsScope = ListPool<float3>.Get(out var chainTails);

                Transform currentWearableParent = null;
                Transform currentAvatarParent = null;

                foreach (SpringBoneData springBone in wearable.SpringBones)
                {
                    // Skip any non-root bones that appear before the first root in this wearable.
                    // Without a root we have no parent transforms to drive the chain.
                    if (!springBone.IsRoot && currentWearableParent == null)
                        continue;

                    springBoneTransforms.Add(springBone.ManagedTransform);
                    springBone.ManagedTransform.localRotation = springBone.InitialLocalRotation;

                    if (springBone.IsRoot)
                    {
                        if (chainJoints.Count > 0)
                        {
                            FlushChain(slots, chainJoints, chainConfigs, chainTails, currentWearableParent, currentAvatarParent);
                            chainJoints.Clear();
                            chainConfigs.Clear();
                            chainTails.Clear();
                        }

                        currentWearableParent = springBone.ManagedTransform.parent;
                        currentAvatarParent = skeleton[springBone.AvatarSkeletonParentBoneIndex];
                        currentWearableParent.SetPositionAndRotation(currentAvatarParent.position, currentAvatarParent.rotation);

                        Vector3 parentOfWearableLossyScale = currentWearableParent.parent != null ? currentWearableParent.parent.lossyScale : Vector3.one;
                        Vector3 avatarLossyScale = currentAvatarParent.lossyScale;
                        currentWearableParent.localScale = new Vector3(
                            avatarLossyScale.x / parentOfWearableLossyScale.x,
                            avatarLossyScale.y / parentOfWearableLossyScale.y,
                            avatarLossyScale.z / parentOfWearableLossyScale.z);
                    }

                    chainJoints.Add(springBone.ManagedTransform);
                    chainConfigs.Add(BuildJointConfig(springBone));
                }

                if (chainJoints.Count > 0)
                    FlushChain(slots, chainJoints, chainConfigs, chainTails, currentWearableParent, currentAvatarParent);
            }

            if (springBoneTransforms.Count > 0)
            {
                transformMatrixComponent.bones.Append(springBoneTransforms);
                transformMatrixComponent.IndexInGlobalJobArray = GlobalJobArrayIndex.Uninitialized();
            }
        }

        private void FlushChain(List<SpringBoneSlot> slots,
            List<Transform> chainJoints, List<SpringBoneJointConfig> chainConfigs, List<float3> chainTails,
            Transform wearableParent, Transform avatarParent)
        {
            int slotIndex = RegisterChain(chainJoints, chainConfigs, chainTails);
            slots.Add(new SpringBoneSlot
            {
                SlotIndex = slotIndex,
                WearableParent = wearableParent,
                AvatarParent = avatarParent,
            });
        }

        private int RegisterChain(List<Transform> joints, List<SpringBoneJointConfig> configs, List<float3> tails)
        {
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

            return springBoneService.RegisterSpring(joints, configs, tails);
        }

        private static void ComputeInitialTailPositions(List<Transform> joints, List<float3> tails)
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

        private static SpringBoneJointConfig BuildJointConfig(SpringBoneData data) =>
            new ()
            {
                Stiffness = data.Stiffness,
                Drag = data.Drag,
                GravityDir = data.GravityDir,
                GravityPower = data.GravityPower,
                LocalRotation = data.InitialLocalRotation,
            };

        private void UnregisterSlots(List<SpringBoneSlot> slots)
        {
            foreach (SpringBoneSlot slot in slots)
                springBoneService.UnregisterSpring(slot.SlotIndex);

            slots.Clear();
        }

        private void CleanUp(ref SpringBoneRegistrationComponent registration)
        {
            if (registration.Slots != null)
            {
                UnregisterSlots(registration.Slots);
                ListPool<SpringBoneSlot>.Release(registration.Slots);
                registration.Slots = null;
            }
        }
    }
}
