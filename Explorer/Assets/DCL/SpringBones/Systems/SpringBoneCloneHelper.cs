using DCL.AvatarRendering.Loading.Assets;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SpringBones
{
    /// <summary>
    ///     Creates pooled clone transforms that mirror spring bone chains under the avatar skeleton.
    ///     Originals stay untouched in the wearable hierarchy — clones are used for simulation and skinning.
    /// </summary>
    public static class SpringBoneCloneHelper
    {
        private static readonly Dictionary<string, Transform> BONE_LOOKUP = new (StringComparer.Ordinal);

        /// <summary>
        ///     For each spring bone chain root, clones the entire chain under the corresponding avatar
        ///     skeleton bone. Appends clone transforms to the provided list and populates the original-to-clone mapping.
        /// </summary>
        public static void CloneSpringBoneChains(IList<CachedAttachment> wearables,
            Transform[] avatarSkeletonBones,
            IComponentPool<Transform> transformPool,
            Dictionary<Transform, Transform> originalToClone,
            List<Transform> outClones)
        {
            try
            {
                foreach (Transform bone in avatarSkeletonBones)
                    if (bone != null) BONE_LOOKUP.TryAdd(bone.name, bone);

                foreach (CachedAttachment wearable in wearables)
                foreach (SpringBoneData springBone in wearable.SpringBones)
                {
                    if (!springBone.IsChainRoot) continue;

                    if (!BONE_LOOKUP.TryGetValue(springBone.SkeletonParentName, out Transform skeletonParent))
                    {
                        ReportHub.LogError(ReportCategory.AVATAR, $"Spring bone '{springBone.Transform.name}' could not find avatar skeleton bone '{springBone.SkeletonParentName}'");
                        continue;
                    }

                    CloneChain(springBone.Transform, skeletonParent, wearable.SpringBones, transformPool, originalToClone, outClones);
                }
            }
            finally
            {
                BONE_LOOKUP.Clear();
            }
        }

        private static void CloneChain(Transform originalRoot,
            Transform skeletonParent,
            SpringBoneData[] allSpringBones,
            IComponentPool<Transform> transformPool,
            Dictionary<Transform, Transform> originalToClone,
            List<Transform> allClones)
        {
            Transform cloneRoot = CloneSingleBone(originalRoot, skeletonParent, transformPool, originalToClone, allClones);
            CloneChildren(originalRoot, cloneRoot, allSpringBones, transformPool, originalToClone, allClones);
        }

        private static void CloneChildren(Transform originalParent,
            Transform cloneParent,
            SpringBoneData[] allSpringBones,
            IComponentPool<Transform> transformPool,
            Dictionary<Transform, Transform> originalToClone,
            List<Transform> allClones)
        {
            for (int i = 0; i < originalParent.childCount; i++)
            {
                Transform originalChild = originalParent.GetChild(i);

                if (!IsSpringBone(originalChild, allSpringBones)) continue;

                Transform cloneChild = CloneSingleBone(originalChild, cloneParent, transformPool, originalToClone, allClones);
                CloneChildren(originalChild, cloneChild, allSpringBones, transformPool, originalToClone, allClones);
            }
        }

        private static Transform CloneSingleBone(Transform original,
            Transform parent,
            IComponentPool<Transform> transformPool,
            Dictionary<Transform, Transform> originalToClone,
            List<Transform> allClones)
        {
            Transform clone = transformPool.Get();

            clone.SetParent(parent, false);
            clone.localPosition = original.localPosition;
            clone.localRotation = original.localRotation;
            clone.localScale = original.localScale;
#if UNITY_EDITOR
            clone.gameObject.name = original.name;
#endif

            originalToClone[original] = clone;
            allClones.Add(clone);

            return clone;
        }

        private static bool IsSpringBone(Transform t,
            SpringBoneData[] springBones)
        {
            foreach (SpringBoneData springBone in springBones)
                if (springBone.Transform == t) return true;

            return false;
        }
    }
}
