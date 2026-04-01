using DCL.AvatarRendering.Loading.Assets;
using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using UniGLTF.SpringBoneJobs.Blittables;
using UniGLTF.SpringBoneJobs.InputPorts;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.SpringBones
{
    public static class SpringBoneBufferBuilder
    {
        public static FastSpringBoneBuffer Build(Transform modelRoot, IList<CachedAttachment> wearables)
        {
            using var transformToSpringBoneScope = DictionaryPool<Transform, SpringBoneData>.Get(out var transformToSpringBone);
            using var packedRootsScope = ListPool<Transform>.Get(out var packedRoots);

            foreach (CachedAttachment wearable in wearables)
            foreach (SpringBoneData springBone in wearable.SpringBones)
            {
                transformToSpringBone[springBone.ManagedTransform] = springBone;
                if (springBone.IsRoot) packedRoots.Add(springBone.ManagedTransform);
            }

            if (packedRoots.Count == 0) return null;

            FastSpringBoneSpring[] springs = BuildSprings(transformToSpringBone, packedRoots);

            return springs.Length > 0 ? new FastSpringBoneBuffer(modelRoot, springs) : null;
        }

        private static FastSpringBoneSpring[] BuildSprings(Dictionary<Transform, SpringBoneData> transformToSpringBone, List<Transform> packedRoots)
        {
            using var springsScope = ListPool<FastSpringBoneSpring>.Get(out var springs);
            using var jointsScope = ListPool<FastSpringBoneJoint>.Get(out var joints);

            foreach (Transform root in packedRoots)
            {
                joints.Clear();
                BuildJointChain(root, transformToSpringBone, joints);

                if (joints.Count < 2)
                {
                    ReportHub.LogError(ReportCategory.AVATAR, $"Spring bone chain rooted at '{root.name}' has less than 2 joints, skipping");
                    continue;
                }

                springs.Add(new FastSpringBoneSpring
                {
                    center = null,
                    joints = joints.ToArray(),
                    colliders = Array.Empty<FastSpringBoneCollider>(),
                });
            }

            return springs.Count > 0 ? springs.ToArray() : Array.Empty<FastSpringBoneSpring>();
        }

        private static void BuildJointChain(Transform transform, Dictionary<Transform, SpringBoneData> transformToSpringBone, List<FastSpringBoneJoint> joints)
        {
            while (transform != null && transformToSpringBone.TryGetValue(transform, out SpringBoneData data))
            {
                joints.Add(new FastSpringBoneJoint
                {
                    Transform = transform,
                    Joint = new BlittableJointMutable(stiffnessForce: data.Stiffness, gravityPower: data.GravityPower, gravityDir: data.GravityDir, dragForce: data.Drag, radius: data.HitRadius),
                    DefaultLocalRotation = data.InitialLocalRotation,
                });

                transform = transform.childCount > 0 ? transform.GetChild(0) : null;
            }
        }
    }
}
