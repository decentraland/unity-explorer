using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.Export
{
    public class BoneRemapper
    {
        private readonly ExportSkeletonMapping targetSkeleton;
        private readonly Dictionary<string, Transform> cachedMappings = new();

        private int totalBonesMapped;
        private int totalBonesUnmapped;

        public BoneRemapper(ExportSkeletonMapping targetSkeleton)
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
                    totalBonesUnmapped++;
                    continue;
                }

                if (cachedMappings.TryGetValue(boneName, out var cachedBone))
                {
                    targetBones[i] = cachedBone;
                    totalBonesMapped++;
                    continue;
                }

                if (targetSkeleton.TryGetByBoneName(boneName, out var targetBone))
                {
                    targetBones[i] = targetBone;
                    cachedMappings[boneName] = targetBone;
                    totalBonesMapped++;
                }
                else
                {
                    targetBones[i] = defaultBone;
                    cachedMappings[boneName] = defaultBone;
                    totalBonesUnmapped++;
                    Debug.LogWarning("BoneRemapper: Could not map bone '" + boneName + "', using Hips as fallback");
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

            var pathParts = originalParentPath.Split('/');

            for (int i = pathParts.Length - 1; i >= 0; i--)
            {
                string boneName = pathParts[i];

                if (targetSkeleton.TryGetByBoneName(boneName, out var targetBone))
                    return targetBone;
            }

            foreach (var part in pathParts)
            {
                var targetBone = TryMatchCommonBonePatterns(part);
                if (targetBone != null)
                    return targetBone;
            }

            return targetSkeleton.GetByHumanBone(HumanBodyBones.Hips);
        }

        private Transform TryMatchCommonBonePatterns(string name)
        {
            string lowerName = name.ToLowerInvariant();

            if (lowerName.Contains("head") || lowerName.Contains("hat") || lowerName.Contains("glasses") ||
                lowerName.Contains("mask") || lowerName.Contains("helmet") || lowerName.Contains("hair"))
            {
                return targetSkeleton.GetByHumanBone(HumanBodyBones.Head);
            }

            if (lowerName.Contains("lefthand") || lowerName.Contains("l_hand") || lowerName.Contains("hand_l"))
                return targetSkeleton.GetByHumanBone(HumanBodyBones.LeftHand);

            if (lowerName.Contains("righthand") || lowerName.Contains("r_hand") || lowerName.Contains("hand_r"))
                return targetSkeleton.GetByHumanBone(HumanBodyBones.RightHand);

            if (lowerName.Contains("leftfoot") || lowerName.Contains("l_foot") || lowerName.Contains("foot_l"))
                return targetSkeleton.GetByHumanBone(HumanBodyBones.LeftFoot);

            if (lowerName.Contains("rightfoot") || lowerName.Contains("r_foot") || lowerName.Contains("foot_r"))
                return targetSkeleton.GetByHumanBone(HumanBodyBones.RightFoot);

            if (lowerName.Contains("chest") || lowerName.Contains("torso") || lowerName.Contains("body"))
                return targetSkeleton.GetByHumanBone(HumanBodyBones.Chest);

            if (lowerName.Contains("hip") || lowerName.Contains("pelvis") || lowerName.Contains("waist"))
                return targetSkeleton.GetByHumanBone(HumanBodyBones.Hips);

            return null;
        }

        public (int mapped, int unmapped) GetStatistics()
        {
            return (totalBonesMapped, totalBonesUnmapped);
        }

        public void LogMappingReport()
        {
            Debug.Log("BoneRemapper Report: " + totalBonesMapped + " bones mapped, " + totalBonesUnmapped + " unmapped");
        }
    }
}
