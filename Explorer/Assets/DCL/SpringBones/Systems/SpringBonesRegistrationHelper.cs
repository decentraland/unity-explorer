using DCL.AvatarRendering.Loading.Assets;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.SpringBones
{
    public static class SpringBonesRegistrationHelper
    {
        public static void RegisterSprings(SpringBoneService service,
            SkinnedMeshRenderer smr,
            List<CachedAttachment> wearables,
            in SpringBoneRegistrationComponent registration,
            List<Transform> result)
        {
            using var jointsScope = ListPool<Transform>.Get(out var joints);
            using var configsScope = ListPool<BlittableJointConfig>.Get(out var configs);
            using var tailsScope = ListPool<float3>.Get(out var tails);

            foreach (CachedAttachment wearable in wearables)
            foreach (SpringBoneData springBone in wearable.SpringBones)
            {
                // Return to caller
                result.Add(springBone.ManagedTransform);

                // Enforce initial local rotation (will change due to pooling)
                springBone.ManagedTransform.localRotation = springBone.InitialLocalRotation;

                if (!springBone.IsRoot) continue;

                AddSyncedBone(springBone, smr, registration);

                CollectSpringBoneData(wearable.SpringBones, springBone, joints, configs, tails);
                int slot = service.RegisterSpring(joints, configs, tails);
                registration.Slots.Add(slot);

                joints.Clear();
                configs.Clear();
                tails.Clear();
            }
        }

        private static void AddSyncedBone(SpringBoneData root, SkinnedMeshRenderer smr, in SpringBoneRegistrationComponent registration)
        {
            Transform wearableParent = root.ManagedTransform.parent;
            Transform avatarParent = smr.bones[root.AvatarSkeletonParentBoneIndex];
            wearableParent.SetPositionAndRotation(avatarParent.position, avatarParent.rotation);

            // Keep synced every frame
            registration.SyncedBones.Add((wearableParent, avatarParent));

            // Wearable lossy scale needs to match the one that it would have if it was within the avatar hierarchy
            // NOTE this is only happening for wearables with spring bones, so no danger in touching the object that will bo gack to the pool
            Vector3 avatarScale = avatarParent.lossyScale;
            Vector3 wearableScale = wearableParent.parent != null ? wearableParent.parent.lossyScale : Vector3.one;
            wearableParent.localScale = new Vector3(avatarScale.x / wearableScale.x, avatarScale.y / wearableScale.y, avatarScale.z / wearableScale.z);
        }

        private static void CollectSpringBoneData(SpringBoneData[] springBones,
            in SpringBoneData root,
            List<Transform> joints,
            List<BlittableJointConfig> configs,
            List<float3> tails)
        {
            CollectChain(springBones, root, joints, configs);

            if (joints.Count < 2) return;

            ComputeBoneGeometry(joints, configs);

            for (int i = 0; i < joints.Count; i++)
            {
                Vector3 tail = i + 1 < joints.Count ? joints[i + 1].position : joints[i].position;
                tails.Add(tail);
            }
        }

        private static void CollectChain(SpringBoneData[] springBones, SpringBoneData root, List<Transform> joints, List<BlittableJointConfig> configs)
        {
            joints.Add(root.ManagedTransform);
            configs.Add(MakeConfig(root));

            Transform current = root.ManagedTransform;
            foreach (var springBone in springBones)
            {
                // Walks the chain
                // Will skip roots entirely (we already have the root)
                // Progresses through the chain by parent matching (parent != current indicates a new chain started)
                if (springBone.IsRoot || springBone.ManagedTransform.parent != current) continue;

                joints.Add(springBone.ManagedTransform);
                configs.Add(MakeConfig(springBone));
                current = springBone.ManagedTransform;
            }
        }

        private static BlittableJointConfig MakeConfig(SpringBoneData data) =>
            new ()
            {
                Stiffness = data.Stiffness,
                Drag = data.Drag,
                GravityDir = data.GravityDir,
                GravityPower = data.GravityPower,
                LocalRotation = data.InitialLocalRotation,
            };

        private static void ComputeBoneGeometry(List<Transform> joints, List<BlittableJointConfig> configs)
        {
            for (int i = 0; i < joints.Count; i++)
            {
                // For each joint we need to compute its axis and length

                var config = configs[i];

                if (i + 1 < joints.Count)
                {
                    Transform tail = joints[i + 1];
                    float3 tip = (float3)tail.localPosition * tail.lossyScale;
                    float length = math.length(tip);
                    config.BoneAxis = length > 0 ? tip / length : math.up();
                    config.Length = length;
                }
                else
                {
                    // Arbitrary default values for the last tail, which is not simulated anyway
                    config.BoneAxis = math.up();
                    config.Length = 0.1f;
                }

                configs[i] = config;
            }
        }
    }
}
