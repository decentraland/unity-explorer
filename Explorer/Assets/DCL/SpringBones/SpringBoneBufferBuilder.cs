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
    /// <summary>
    ///     Constructs <see cref="FastSpringBoneBuffer"/> instances from wearable spring bone data.
    ///     Supports an optional original-to-clone transform mapping so the simulation
    ///     operates on clone transforms under the avatar skeleton rather than the originals.
    /// </summary>
    public static class SpringBoneBufferBuilder
    {
        /// <summary>
        ///     Builds a <see cref="FastSpringBoneBuffer"/> for the given wearables. When
        ///     <paramref name="originalToClone"/> is provided, clone transforms are used
        ///     for chain roots and joints instead of the originals in the wearable hierarchy.
        ///     Returns null if no spring bone chains are found.
        /// </summary>
        public static FastSpringBoneBuffer Build(Transform modelRoot, IList<CachedAttachment> wearables, Dictionary<Transform, Transform> originalToClone = null)
        {
            using var springBoneMapScope = DictionaryPool<Transform, SpringBoneData>.Get(out var springBoneMap);
            using var chainRootsScope = ListPool<SpringBoneData>.Get(out var chainRoots);

            CollectSpringBoneData(wearables, springBoneMap, chainRoots, originalToClone);

            if (chainRoots.Count == 0) return null;

            FastSpringBoneSpring[] springs = BuildSprings(springBoneMap, chainRoots, originalToClone);

            return springs.Length > 0 ? new FastSpringBoneBuffer(modelRoot, springs) : null;
        }

        /// <summary>
        ///     Collects spring bone data from all wearables into a flat map keyed by (clone) transform,
        ///     and identifies chain roots for spring construction.
        /// </summary>
        private static void CollectSpringBoneData(IList<CachedAttachment> wearables,
            Dictionary<Transform, SpringBoneData> springBoneMap, List<SpringBoneData> chainRoots,
            Dictionary<Transform, Transform> originalToClone)
        {
            foreach (CachedAttachment wearable in wearables)
            {
                foreach (SpringBoneData sbd in wearable.SpringBones)
                {
                    Transform key = Resolve(sbd.Transform, originalToClone);
                    springBoneMap[key] = sbd;

                    if (sbd.IsChainRoot) chainRoots.Add(sbd);
                }
            }
        }

        /// <summary>
        ///     Builds <see cref="FastSpringBoneSpring"/> arrays by walking each chain root's
        ///     transform hierarchy to reconstruct joint chains.
        /// </summary>
        private static FastSpringBoneSpring[] BuildSprings(
            Dictionary<Transform, SpringBoneData> springBoneMap, List<SpringBoneData> chainRoots,
            Dictionary<Transform, Transform> originalToClone)
        {
            using var springsScope = ListPool<FastSpringBoneSpring>.Get(out var springs);
            using var jointsScope = ListPool<FastSpringBoneJoint>.Get(out var joints);

            foreach (SpringBoneData root in chainRoots)
            {
                joints.Clear();
                Transform rootTransform = Resolve(root.Transform, originalToClone);
                BuildJointChain(rootTransform, springBoneMap, joints);

                if (joints.Count < 2)
                {
                    ReportHub.LogError(ReportCategory.AVATAR, $"Spring bone chain rooted at '{rootTransform.name}' has fewer than 2 joints, skipping");
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

        /// <summary>
        ///     Walks the transform hierarchy starting from <paramref name="current"/>, collecting
        ///     joints for each spring bone found in the map. Stops when no spring bone child is found.
        /// </summary>
        private static void BuildJointChain(Transform current, Dictionary<Transform, SpringBoneData> springBoneMap, List<FastSpringBoneJoint> joints)
        {
            while (current != null && springBoneMap.TryGetValue(current, out SpringBoneData data))
            {
                joints.Add(new FastSpringBoneJoint
                {
                    Transform = current,
                    Joint = new BlittableJointMutable(stiffnessForce: data.Stiffness, gravityPower: data.GravityPower, gravityDir: data.GravityDir, dragForce: data.Drag, radius: data.HitRadius),
                    DefaultLocalRotation = data.DefaultLocalRotation,
                });

                Transform next = null;

                for (var i = 0; i < current.childCount; i++)
                {
                    Transform child = current.GetChild(i);

                    if (springBoneMap.ContainsKey(child)) { next = child; break; }
                }

                current = next;
            }
        }

        private static Transform Resolve(Transform original, Dictionary<Transform, Transform> originalToClone)
        {
            if (originalToClone != null && originalToClone.TryGetValue(original, out Transform clone)) return clone;
            return original;
        }
    }
}
