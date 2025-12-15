using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.Export
{
    /// <summary>
    /// Contains all mapping data for a bone in the export skeleton
    /// </summary>
    public readonly struct ExportBoneData
    {
        public readonly HumanBodyBones HumanBone;
        public readonly Transform TargetTransform;
        public readonly string SourceBoneName;

        public ExportBoneData(HumanBodyBones humanBone, Transform targetTransform, string sourceBoneName)
        {
            HumanBone = humanBone;
            TargetTransform = targetTransform;
            SourceBoneName = sourceBoneName;
        }
    }

    public class ExportSkeletonMapping
    {
        public readonly GameObject Root;
        
        private readonly List<ExportBoneData> bones = new();
        private readonly Dictionary<HumanBodyBones, Transform> humanBoneToTransform = new();
        private readonly Dictionary<string, Transform> boneNameToTransform = new(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyList<ExportBoneData> Bones => bones;

        public ExportSkeletonMapping(GameObject root)
        {
            Root = root;
        }

        public void AddBone(ExportBoneData boneData)
        {
            bones.Add(boneData);
            humanBoneToTransform[boneData.HumanBone] = boneData.TargetTransform;
            boneNameToTransform[boneData.SourceBoneName] = boneData.TargetTransform;
        }

        public bool TryGetByBoneName(string boneName, out Transform transform)
            => boneNameToTransform.TryGetValue(boneName, out transform);

        public Transform GetByHumanBone(HumanBodyBones humanBone)
            => humanBoneToTransform.GetValueOrDefault(humanBone);

        public Dictionary<HumanBodyBones, Transform> ToHumanBoneDictionary()
            => new(humanBoneToTransform);
    }
}