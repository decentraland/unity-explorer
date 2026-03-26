using DCL.AvatarRendering.Loading.Assets;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.SpringBones
{
    /// <summary>
    ///     Creates pooled clone transforms that mirror spring bone chains under the avatar skeleton.
    ///     Originals stay untouched in the wearable hierarchy — clones are used for simulation and skinning.
    /// </summary>
    public static class SpringBoneCloneHelper
    {
        /// <summary>
        ///     For each spring bone chain root, clones the entire chain under the corresponding avatar
        ///     skeleton bone. Returns the clone transforms and populates the original-to-clone mapping.
        /// </summary>
        public static Transform[] CloneSpringBoneChains(IList<CachedAttachment> wearables, Transform[] avatarSkeletonBones,
            IComponentPool<Transform> transformPool, Dictionary<Transform, Transform> originalToClone)
        {
            using var bonesByNameScope = DictionaryPool<string, Transform>.Get(out var bonesByName);
            using var allClonesScope = ListPool<Transform>.Get(out var allClones);

            foreach (Transform bone in avatarSkeletonBones)
                if (bone != null) bonesByName.TryAdd(bone.name, bone);

            foreach (CachedAttachment wearable in wearables)
            {
                foreach (SpringBoneData sbd in wearable.SpringBones)
                {
                    if (!sbd.IsChainRoot) continue;

                    if (!bonesByName.TryGetValue(sbd.SkeletonParentName, out Transform skeletonParent))
                    {
                        ReportHub.LogError(ReportCategory.AVATAR, $"Spring bone '{sbd.Transform.name}' could not find avatar skeleton bone '{sbd.SkeletonParentName}'");
                        continue;
                    }

                    CloneChain(sbd.Transform, skeletonParent, wearable.SpringBones, transformPool, originalToClone, allClones);
                }
            }

            return allClones.Count > 0 ? allClones.ToArray() : Array.Empty<Transform>();
        }

        /// <summary>
        ///     Collects all spring bone transforms from wearables into a flat array.
        /// </summary>
        public static Transform[] CollectSpringBones(IList<CachedAttachment> wearables)
        {
            int totalCount = 0;

            foreach (CachedAttachment wearable in wearables)
                totalCount += wearable.SpringBones.Length;

            if (totalCount == 0) return Array.Empty<Transform>();

            var result = new Transform[totalCount];
            int offset = 0;

            foreach (CachedAttachment wearable in wearables)
            {
                foreach (SpringBoneData bone in wearable.SpringBones)
                    result[offset++] = bone.Transform;
            }

            return result;
        }

        /// <summary>
        ///     Returns clone transforms to the pool.
        /// </summary>
        public static void ReleaseClones(Transform[] clones, IComponentPool<Transform> transformPool)
        {
            if (clones == null) return;

            foreach (Transform clone in clones)
                if (clone != null) transformPool.Release(clone);
        }

        private static void CloneChain(Transform originalRoot, Transform skeletonParent, SpringBoneData[] allSpringBones,
            IComponentPool<Transform> transformPool, Dictionary<Transform, Transform> originalToClone, List<Transform> allClones)
        {
            Transform cloneRoot = CloneSingleBone(originalRoot, skeletonParent, transformPool, originalToClone, allClones);
            CloneChildren(originalRoot, cloneRoot, allSpringBones, transformPool, originalToClone, allClones);
        }

        private static void CloneChildren(Transform originalParent, Transform cloneParent, SpringBoneData[] allSpringBones,
            IComponentPool<Transform> transformPool, Dictionary<Transform, Transform> originalToClone, List<Transform> allClones)
        {
            for (var i = 0; i < originalParent.childCount; i++)
            {
                Transform originalChild = originalParent.GetChild(i);

                if (!IsSpringBone(originalChild, allSpringBones)) continue;

                Transform cloneChild = CloneSingleBone(originalChild, cloneParent, transformPool, originalToClone, allClones);
                CloneChildren(originalChild, cloneChild, allSpringBones, transformPool, originalToClone, allClones);
            }
        }

        private static Transform CloneSingleBone(Transform original, Transform parent, IComponentPool<Transform> transformPool,
            Dictionary<Transform, Transform> originalToClone, List<Transform> allClones)
        {
            Transform clone = transformPool.Get();
            clone.SetParent(parent, false);
            clone.localPosition = original.localPosition;
            clone.localRotation = original.localRotation;
            clone.localScale = original.localScale;
            clone.gameObject.name = original.name;
            originalToClone[original] = clone;
            allClones.Add(clone);
            return clone;
        }

        private static bool IsSpringBone(Transform t, SpringBoneData[] springBones)
        {
            foreach (SpringBoneData sbd in springBones)
                if (sbd.Transform == t) return true;

            return false;
        }
    }
}
