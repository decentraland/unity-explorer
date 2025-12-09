using System.Collections.Generic;
using System.Linq;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Loading.Assets;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using Vector3 = UnityEngine.Vector3;

namespace DCL.AvatarRendering.Export
{
    public class ExportSkeletonBuilder
    {
        public ExportSkeletonMapping BuildFromAvatarBase(AvatarBase avatarBase, IReadOnlyList<CachedAttachment> instantiatedWearables)
        {
            var armature = avatarBase.Armature;
            var armatureScale = armature.localScale; // Expected (0.01, 0.01, 0.01)

            if (instantiatedWearables.Count <= 0 || instantiatedWearables[0].OriginalAsset.MainAsset == null)
                return null;
            
            var duplicateRoot = new GameObject("DCL_Avatar_Export").transform;
            duplicateRoot.position = armature.position;
            duplicateRoot.rotation = armature.rotation;
            
            var bones = InstantiateBones(instantiatedWearables[0].OriginalAsset.MainAsset.transform, duplicateRoot).transform;
            bones.SetParent(duplicateRoot);
            
            duplicateRoot.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            ApplyParentScaleToChildren(duplicateRoot);

            duplicateRoot.gameObject.AddComponent<Animator>();

            var mapping = new ExportSkeletonMapping(duplicateRoot.gameObject, armatureScale);

            var boneDefinitions = GetBoneDefinitions(avatarBase);

            mapping.AddBone(new ExportBoneData(HumanBodyBones.Hips, null, bones, bones.name));
            MapBonesRecursive(mapping, bones, boneDefinitions);

            var boneRenderer = duplicateRoot.gameObject.AddComponent<BoneRenderer>();
            boneRenderer.transforms = mapping.Bones.Select(x => x.TargetTransform).ToArray();

            return mapping;

            void MapBonesRecursive(ExportSkeletonMapping mapping, Transform parent, List<BoneDefinition> boneDefinitions)
            {
                foreach (Transform bone in parent)
                {
                    if (!BoneDefinition.TryGetBoneDefByName(boneDefinitions, bone.name, out var boneDefinition))
                        continue;

                    mapping.AddBone(new ExportBoneData(
                        boneDefinition.HumanBone, 
                        boneDefinition.Parent, 
                        bone, 
                        bone.name
                    ));

                    // Recurse into this bone's children
                    MapBonesRecursive(mapping, bone, boneDefinitions);
                }
            }
        }

        private GameObject InstantiateBones(Transform sourceRoot, Transform parent)
        {
            string hipsName = "Avatar_Hips";
            
            var hipsTransform = FindChildRecursive(sourceRoot, hipsName);
            var hipsInstance = Object.Instantiate(hipsTransform.gameObject, parent);
            hipsInstance.transform.localPosition = hipsTransform.localPosition;
            hipsInstance.name = hipsTransform.name;
            
            return hipsInstance;
            
            Transform FindChildRecursive(Transform parent, string name)
            {
                if (parent.name == name)
                    return parent;

                foreach (Transform child in parent)
                {
                    Transform found = FindChildRecursive(child, name);
                    if (found != null)
                        return found;
                }

                return null;
            }
        }
        
        public static void ApplyParentScaleToChildren(Transform parent)
        {
            var childPositions = new List<(Transform child, Vector3 position)>();
            StoreChildPositionsRecursive(parent, childPositions);

            parent.localScale = Vector3.one;

            foreach (var (child, position) in childPositions)
            {
                child.position = position;
            }

            return;
            
            void StoreChildPositionsRecursive(Transform parent, List<(Transform, Vector3)> list)
            {
                foreach (Transform child in parent)
                {
                    list.Add((child, child.position));
                    StoreChildPositionsRecursive(child, list);
                }
            }
        }

        private List<BoneDefinition> GetBoneDefinitions(AvatarBase avatarBase)
        {
            return new List<BoneDefinition>
            {
                new(HumanBodyBones.Hips, null, avatarBase.HipAnchorPoint, "Avatar_Hips"),
                new(HumanBodyBones.Spine, HumanBodyBones.Hips, avatarBase.SpineAnchorPoint, "Avatar_Spine"),
                new(HumanBodyBones.Chest, HumanBodyBones.Spine, avatarBase.Spine1AnchorPoint, "Avatar_Spine1"),
                new(HumanBodyBones.UpperChest, HumanBodyBones.Chest, avatarBase.Spine2AnchorPoint, "Avatar_Spine2"),
                new(HumanBodyBones.Neck, HumanBodyBones.UpperChest, avatarBase.NeckAnchorPoint, "Avatar_Neck"),
                new(HumanBodyBones.Head, HumanBodyBones.Neck, avatarBase.HeadAnchorPoint, "Avatar_Head"),

                new(HumanBodyBones.LeftShoulder, HumanBodyBones.UpperChest, avatarBase.LeftShoulderAnchorPoint, "Avatar_LeftShoulder"),
                new(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftShoulder, avatarBase.LeftArmAnchorPoint, "Avatar_LeftArm"),
                new(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftUpperArm, avatarBase.LeftForearmAnchorPoint, "Avatar_LeftForeArm"),
                new(HumanBodyBones.LeftHand, HumanBodyBones.LeftLowerArm, avatarBase.LeftHandAnchorPoint, "Avatar_LeftHand"),

                new(HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftHand, avatarBase.LeftHandFingers.ThumbProximalAnchorPoint, "Avatar_LeftHandThumb1"),
                new(HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbProximal, avatarBase.LeftHandFingers.ThumbIntermediateAnchorPoint, "Avatar_LeftHandThumb2"),
                new(HumanBodyBones.LeftThumbDistal, HumanBodyBones.LeftThumbIntermediate, avatarBase.LeftHandFingers.ThumbDistalAnchorPoint, "Avatar_LeftHandThumb3"),
                new(HumanBodyBones.LeftThumbDistal, HumanBodyBones.LeftThumbDistal, null, "Avatar_LeftHandThumb4"),
                new(HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftHand, avatarBase.LeftHandFingers.IndexProximalAnchorPoint, "Avatar_LeftHandIndex1"),
                new(HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexProximal, avatarBase.LeftHandFingers.IndexIntermediateAnchorPoint, "Avatar_LeftHandIndex2"),
                new(HumanBodyBones.LeftIndexDistal, HumanBodyBones.LeftIndexIntermediate, avatarBase.LeftHandFingers.IndexDistalAnchorPoint, "Avatar_LeftHandIndex3"),
                new(HumanBodyBones.LeftIndexDistal, HumanBodyBones.LeftIndexDistal, null, "Avatar_LeftHandIndex4"),
                new(HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftHand, avatarBase.LeftHandFingers.MiddleProximalAnchorPoint, "Avatar_LeftHandMiddle1"),
                new(HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleProximal, avatarBase.LeftHandFingers.MiddleIntermediateAnchorPoint, "Avatar_LeftHandMiddle2"),
                new(HumanBodyBones.LeftMiddleDistal, HumanBodyBones.LeftMiddleIntermediate, avatarBase.LeftHandFingers.MiddleDistalAnchorPoint, "Avatar_LeftHandMiddle3"),
                new(HumanBodyBones.LeftMiddleDistal, HumanBodyBones.LeftMiddleDistal, null, "Avatar_LeftHandMiddle4"),
                new(HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftHand, avatarBase.LeftHandFingers.RingProximalAnchorPoint, "Avatar_LeftHandRing1"),
                new(HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingProximal, avatarBase.LeftHandFingers.RingIntermediateAnchorPoint, "Avatar_LeftHandRing2"),
                new(HumanBodyBones.LeftRingDistal, HumanBodyBones.LeftRingIntermediate, avatarBase.LeftHandFingers.RingDistalAnchorPoint, "Avatar_LeftHandRing3"),
                new(HumanBodyBones.LeftRingDistal, HumanBodyBones.LeftRingDistal, null, "Avatar_LeftHandRing4"),
                new(HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftHand, avatarBase.LeftHandFingers.LittleProximalAnchorPoint, "Avatar_LeftHandPinky1"),
                new(HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleProximal, avatarBase.LeftHandFingers.LittleIntermediateAnchorPoint, "Avatar_LeftHandPinky2"),
                new(HumanBodyBones.LeftLittleDistal, HumanBodyBones.LeftLittleIntermediate, avatarBase.LeftHandFingers.LittleDistalAnchorPoint, "Avatar_LeftHandPinky3"),
                new(HumanBodyBones.LeftLittleDistal, HumanBodyBones.LeftLittleDistal, null, "Avatar_LeftHandPinky4"),

                new(HumanBodyBones.RightShoulder, HumanBodyBones.UpperChest, avatarBase.RightShoulderAnchorPoint, "Avatar_RightShoulder"),
                new(HumanBodyBones.RightUpperArm, HumanBodyBones.RightShoulder, avatarBase.RightArmAnchorPoint, "Avatar_RightArm"),
                new(HumanBodyBones.RightLowerArm, HumanBodyBones.RightUpperArm, avatarBase.RightForearmAnchorPoint, "Avatar_RightForeArm"),
                new(HumanBodyBones.RightHand, HumanBodyBones.RightLowerArm, avatarBase.RightHandAnchorPoint, "Avatar_RightHand"),

                new(HumanBodyBones.RightThumbProximal, HumanBodyBones.RightHand, avatarBase.RightHandFingers.ThumbProximalAnchorPoint, "Avatar_RightHandThumb1"),
                new(HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbProximal, avatarBase.RightHandFingers.ThumbIntermediateAnchorPoint, "Avatar_RightHandThumb2"),
                new(HumanBodyBones.RightThumbDistal, HumanBodyBones.RightThumbIntermediate, avatarBase.RightHandFingers.ThumbDistalAnchorPoint, "Avatar_RightHandThumb3"),
                new(HumanBodyBones.RightThumbDistal, HumanBodyBones.RightThumbDistal, null, "Avatar_RightHandThumb4"),
                new(HumanBodyBones.RightIndexProximal, HumanBodyBones.RightHand, avatarBase.RightHandFingers.IndexProximalAnchorPoint, "Avatar_RightHandIndex1"),
                new(HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexProximal, avatarBase.RightHandFingers.IndexIntermediateAnchorPoint, "Avatar_RightHandIndex2"),
                new(HumanBodyBones.RightIndexDistal, HumanBodyBones.RightIndexIntermediate, avatarBase.RightHandFingers.IndexDistalAnchorPoint, "Avatar_RightHandIndex3"),
                new(HumanBodyBones.RightIndexDistal, HumanBodyBones.RightIndexDistal, null, "Avatar_RightHandIndex4"),
                new(HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightHand, avatarBase.RightHandFingers.MiddleProximalAnchorPoint, "Avatar_RightHandMiddle1"),
                new(HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleProximal, avatarBase.RightHandFingers.MiddleIntermediateAnchorPoint, "Avatar_RightHandMiddle2"),
                new(HumanBodyBones.RightMiddleDistal, HumanBodyBones.RightMiddleIntermediate, avatarBase.RightHandFingers.MiddleDistalAnchorPoint, "Avatar_RightHandMiddle3"),
                new(HumanBodyBones.RightMiddleDistal, HumanBodyBones.RightMiddleDistal, null, "Avatar_RightHandMiddle4"),
                new(HumanBodyBones.RightRingProximal, HumanBodyBones.RightHand, avatarBase.RightHandFingers.RingProximalAnchorPoint, "Avatar_RightHandRing1"),
                new(HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingProximal, avatarBase.RightHandFingers.RingIntermediateAnchorPoint, "Avatar_RightHandRing2"),
                new(HumanBodyBones.RightRingDistal, HumanBodyBones.RightRingIntermediate, avatarBase.RightHandFingers.RingDistalAnchorPoint, "Avatar_RightHandRing3"),
                new(HumanBodyBones.RightRingDistal, HumanBodyBones.RightRingDistal, null, "Avatar_RightHandRing4"),
                new(HumanBodyBones.RightLittleProximal, HumanBodyBones.RightHand, avatarBase.RightHandFingers.LittleProximalAnchorPoint, "Avatar_RightHandPinky1"),
                new(HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleProximal, avatarBase.RightHandFingers.LittleIntermediateAnchorPoint, "Avatar_RightHandPinky2"),
                new(HumanBodyBones.RightLittleDistal, HumanBodyBones.RightLittleIntermediate, avatarBase.RightHandFingers.LittleDistalAnchorPoint, "Avatar_RightHandPinky3"),
                new(HumanBodyBones.RightLittleDistal, HumanBodyBones.RightLittleDistal, null, "Avatar_RightHandPinky4"),

                new(HumanBodyBones.LeftUpperLeg, HumanBodyBones.Hips, avatarBase.LeftUpLegAnchorPoint, "Avatar_LeftUpLeg"),
                new(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftUpperLeg, avatarBase.LeftLegAnchorPoint, "Avatar_LeftLeg"),
                new(HumanBodyBones.LeftFoot, HumanBodyBones.LeftLowerLeg, avatarBase.LeftFootAnchorPoint, "Avatar_LeftFoot"),
                new(HumanBodyBones.LeftToes, HumanBodyBones.LeftFoot, avatarBase.LeftToeBaseAnchorPoint, "Avatar_LeftToeBase"),

                new(HumanBodyBones.RightUpperLeg, HumanBodyBones.Hips, avatarBase.RightUpLegAnchorPoint, "Avatar_RightUpLeg"),
                new(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightUpperLeg, avatarBase.RightLegAnchorPoint, "Avatar_RightLeg"),
                new(HumanBodyBones.RightFoot, HumanBodyBones.RightLowerLeg, avatarBase.RightFootAnchorPoint, "Avatar_RightFoot"),
                new(HumanBodyBones.RightToes, HumanBodyBones.RightFoot, avatarBase.RightToeBaseAnchorPoint, "Avatar_RightToeBase"),
            };
        }

        private readonly struct BoneDefinition
        {
            public readonly HumanBodyBones HumanBone;
            public readonly HumanBodyBones? Parent;
            public readonly Transform Source;
            public readonly string SourceBoneName;

            public BoneDefinition(HumanBodyBones humanBone, HumanBodyBones? parent, Transform source, string sourceBoneName)
            {
                HumanBone = humanBone;
                Parent = parent;
                Source = source;
                SourceBoneName = sourceBoneName;
            }

            public static bool TryGetBoneDefByName(List<BoneDefinition> boneDefinitions, string name, out BoneDefinition definition)
            {
                foreach (var boneDefinition in boneDefinitions)
                {
                    if (boneDefinition.SourceBoneName != name)
                        continue;

                    definition = boneDefinition;
                    return true;
                }

                definition = default;
                return false;
            }
        }
    }
}
