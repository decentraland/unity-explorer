using DCL.AvatarRendering.Loading.Assets;
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
            Dictionary<Transform, SpringBoneData> springBoneMap = DictionaryPool<Transform, SpringBoneData>.Get();
            List<SpringBoneData> chainRoots = ListPool<SpringBoneData>.Get();

            try
            {
                CollectSpringBoneData(wearables, springBoneMap, chainRoots);

                if (chainRoots.Count == 0)
                    return null;

                FastSpringBoneSpring[] springs = BuildSprings(springBoneMap, chainRoots);

                return springs.Length > 0
                    ? new FastSpringBoneBuffer(modelRoot, springs)
                    : null;
            }
            finally
            {
                DictionaryPool<Transform, SpringBoneData>.Release(springBoneMap);
                ListPool<SpringBoneData>.Release(chainRoots);
            }
        }

        private static void CollectSpringBoneData(IList<CachedAttachment> wearables,
            Dictionary<Transform, SpringBoneData> springBoneMap, List<SpringBoneData> chainRoots)
        {
            for (var w = 0; w < wearables.Count; w++)
            {
                SpringBoneData[] springBones = wearables[w].SpringBones;

                for (var i = 0; i < springBones.Length; i++)
                {
                    SpringBoneData sbd = springBones[i];

                    if (sbd.Transform == null)
                        continue;

                    springBoneMap[sbd.Transform] = sbd;

                    if (sbd.IsChainRoot)
                        chainRoots.Add(sbd);
                }
            }
        }

        private static FastSpringBoneSpring[] BuildSprings(
            Dictionary<Transform, SpringBoneData> springBoneMap, List<SpringBoneData> chainRoots)
        {
            List<FastSpringBoneSpring> springs = ListPool<FastSpringBoneSpring>.Get();
            List<FastSpringBoneJoint> joints = ListPool<FastSpringBoneJoint>.Get();

            try
            {
                for (var r = 0; r < chainRoots.Count; r++)
                {
                    joints.Clear();
                    BuildJointChain(chainRoots[r].Transform, springBoneMap, joints);

                    // FastSpringBoneBuffer requires at least 2 joints (head + tail)
                    if (joints.Count < 2)
                        continue;

                    springs.Add(new FastSpringBoneSpring
                    {
                        center = null,
                        joints = joints.ToArray(),
                        colliders = Array.Empty<FastSpringBoneCollider>(),
                    });
                }

                return springs.Count > 0 ? springs.ToArray() : Array.Empty<FastSpringBoneSpring>();
            }
            finally
            {
                ListPool<FastSpringBoneSpring>.Release(springs);
                ListPool<FastSpringBoneJoint>.Release(joints);
            }
        }

        private static void BuildJointChain(Transform current,
            Dictionary<Transform, SpringBoneData> springBoneMap, List<FastSpringBoneJoint> joints)
        {
            while (current != null && springBoneMap.TryGetValue(current, out SpringBoneData data))
            {
                joints.Add(new FastSpringBoneJoint
                {
                    Transform = current,
                    Joint = new BlittableJointMutable(
                        stiffnessForce: data.Stiffness,
                        gravityPower: data.GravityPower,
                        gravityDir: data.GravityDir,
                        dragForce: data.Drag,
                        radius: data.HitRadius),
                    DefaultLocalRotation = data.DefaultLocalRotation,
                });

                // Walk to the next spring bone child in the chain
                Transform next = null;

                for (var i = 0; i < current.childCount; i++)
                {
                    Transform child = current.GetChild(i);

                    if (springBoneMap.ContainsKey(child))
                    {
                        next = child;
                        break;
                    }
                }

                current = next;
            }
        }
    }
}
