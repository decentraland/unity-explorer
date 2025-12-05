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
        public readonly HumanBodyBones? ParentBone;
        public readonly Transform TargetTransform;
        public readonly string SourceBoneName; // Name used in source rigs (e.g. "LeftArm", "Spine1")

        public ExportBoneData(HumanBodyBones humanBone, HumanBodyBones? parentBone, Transform targetTransform, string sourceBoneName)
        {
            HumanBone = humanBone;
            ParentBone = parentBone;
            TargetTransform = targetTransform;
            SourceBoneName = sourceBoneName;
        }
    }

    public class ExportSkeletonMapping
    {
        public readonly GameObject Root;
        public readonly Vector3 MeshScale;
        
        private readonly List<ExportBoneData> bones = new();
        private readonly Dictionary<HumanBodyBones, Transform> humanBoneToTransform = new();
        private readonly Dictionary<string, Transform> boneNameToTransform = new(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyList<ExportBoneData> Bones => bones;

        public ExportSkeletonMapping(GameObject root, Vector3 meshScale)
        {
            Root = root;
            MeshScale = meshScale;
        }

        public void AddBone(ExportBoneData boneData)
        {
            bones.Add(boneData);
            humanBoneToTransform[boneData.HumanBone] = boneData.TargetTransform;
            boneNameToTransform[boneData.SourceBoneName] = boneData.TargetTransform;
        }

        public bool TryGetByHumanBone(HumanBodyBones humanBone, out Transform transform)
            => humanBoneToTransform.TryGetValue(humanBone, out transform);

        public bool TryGetByBoneName(string boneName, out Transform transform)
            => boneNameToTransform.TryGetValue(boneName, out transform);

        public Transform GetByHumanBone(HumanBodyBones humanBone)
            => humanBoneToTransform.GetValueOrDefault(humanBone);

        public Transform GetByBoneName(string boneName)
            => boneNameToTransform.GetValueOrDefault(boneName);

        public Dictionary<HumanBodyBones, Transform> ToHumanBoneDictionary()
            => new(humanBoneToTransform);

        public int BoneCount => bones.Count;
    }
}