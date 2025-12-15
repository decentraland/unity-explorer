using System;
using System.Collections.Generic;
using DCL.Diagnostics;
using UnityEngine;

namespace DCL.AvatarRendering.Export
{
    public class ExportSkeletonBoneRemapper
    {
        private readonly ExportSkeletonMapping targetSkeleton;
        private readonly Dictionary<string, Transform> cachedMappings = new();

        public ExportSkeletonBoneRemapper(ExportSkeletonMapping targetSkeleton)
        {
            this.targetSkeleton = targetSkeleton;
        }

        public Transform[] RemapBones(Transform[] sourceBones, string[] sourceBoneNames)
        {
            if (sourceBones == null || sourceBoneNames == null)
                return Array.Empty<Transform>();

            var targetBones = new Transform[sourceBones.Length];
            var defaultBone = targetSkeleton.GetByHumanBone(HumanBodyBones.Hips);

            for (int i = 0; i < sourceBones.Length; i++)
            {
                string boneName = sourceBoneNames[i];

                if (string.IsNullOrEmpty(boneName))
                {
                    targetBones[i] = defaultBone;
                    continue;
                }

                if (cachedMappings.TryGetValue(boneName, out var cachedBone))
                {
                    targetBones[i] = cachedBone;
                    continue;
                }

                if (targetSkeleton.TryGetByBoneName(boneName, out var targetBone))
                {
                    targetBones[i] = targetBone;
                    cachedMappings[boneName] = targetBone;
                }
                else
                {
                    targetBones[i] = defaultBone;
                    cachedMappings[boneName] = defaultBone;
                    ReportHub.LogWarning(ReportCategory.AVATAR_EXPORT, $"BoneRemapper: Could not map bone '{boneName}', using Hips as fallback");
                }
            }

            return targetBones;
        }

        public Transform GetTargetBone(string sourceBoneName)
        {
            if (string.IsNullOrEmpty(sourceBoneName))
                return null;

            if (cachedMappings.TryGetValue(sourceBoneName, out var cached))
                return cached;

            if (targetSkeleton.TryGetByBoneName(sourceBoneName, out var target))
            {
                cachedMappings[sourceBoneName] = target;
                return target;
            }

            return null;
        }

        public Transform FindAttachmentBone(string originalParentPath)
        {
            if (string.IsNullOrEmpty(originalParentPath))
                return targetSkeleton.GetByHumanBone(HumanBodyBones.Hips);

            string[] pathParts = originalParentPath.Split('/');

            for (int i = pathParts.Length - 1; i >= 0; i--)
            {
                string boneName = pathParts[i];

                if (targetSkeleton.TryGetByBoneName(boneName, out var targetBone))
                    return targetBone;
            }

            return targetSkeleton.GetByHumanBone(HumanBodyBones.Hips);
        }
    }
}
