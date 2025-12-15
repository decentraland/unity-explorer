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
            if (instantiatedWearables.Count <= 0 || instantiatedWearables[0].OriginalAsset.MainAsset == null)
                return null;
            
            var armature = avatarBase.Armature;
            
            var duplicateRoot = new GameObject("DCL_Avatar_Export").transform;
            duplicateRoot.position = armature.position;
            duplicateRoot.rotation = armature.rotation;
            
            var bones = InstantiateBones(instantiatedWearables[0].OriginalAsset.MainAsset.transform, duplicateRoot).transform;
            bones.SetParent(duplicateRoot);
            
            // DCL skeleton is in 0.01 scale, we need to scale it to uniform (1,1,1) scale.
            duplicateRoot.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            ApplyParentScaleToChildren(duplicateRoot);

            duplicateRoot.gameObject.AddComponent<Animator>();

            var mapping = new ExportSkeletonMapping(duplicateRoot.gameObject);
            var dclToHumanBone = GetBoneDefinitions();
            MapBonesRecursive(dclToHumanBone, mapping, bones);

           // mapping.AddBone(new ExportBoneData(HumanBodyBones.Hips, bones, bones.name));
            

            var boneRenderer = duplicateRoot.gameObject.AddComponent<BoneRenderer>();
            boneRenderer.transforms = mapping.Bones.Select(x => x.TargetTransform).ToArray();

            return mapping;

            void MapBonesRecursive(Dictionary<string, HumanBodyBones> dclToHumanBone, ExportSkeletonMapping mapping, Transform bone)
            {
                if (dclToHumanBone.TryGetValue(bone.name, out var humanBone))
                    mapping.AddBone(new ExportBoneData(humanBone, bone, bone.name));

                foreach (Transform child in bone)
                    MapBonesRecursive(dclToHumanBone, mapping, child);
            }
        }

        private GameObject InstantiateBones(Transform sourceRoot, Transform parent)
        {
            const string HIPS_NAME = "Avatar_Hips";
            
            var hipsTransform = FindChildRecursive(sourceRoot, HIPS_NAME);
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
        
        private static void ApplyParentScaleToChildren(Transform parent)
        {
            var childPositions = new List<(Transform child, Vector3 position)>();
            StoreChildPositionsRecursive(parent, childPositions);

            parent.localScale = Vector3.one;

            foreach (var (child, position) in childPositions)
            {
                child.position = position;
            }

            return;
            
            void StoreChildPositionsRecursive(Transform sourceParent, List<(Transform, Vector3)> list)
            {
                foreach (Transform child in sourceParent)
                {
                    list.Add((child, child.position));
                    StoreChildPositionsRecursive(child, list);
                }
            }
        }

        private Dictionary<string, HumanBodyBones> GetBoneDefinitions()
        {
            return new Dictionary<string, HumanBodyBones>
            {
                ["Avatar_Hips"] = HumanBodyBones.Hips,
                ["Avatar_Spine"] = HumanBodyBones.Spine,
                ["Avatar_Spine1"] = HumanBodyBones.Chest,
                ["Avatar_Spine2"] = HumanBodyBones.UpperChest,
                ["Avatar_Neck"] = HumanBodyBones.Neck,
                ["Avatar_Head"] = HumanBodyBones.Head,

                ["Avatar_LeftShoulder"] = HumanBodyBones.LeftShoulder,
                ["Avatar_LeftArm"] = HumanBodyBones.LeftUpperArm,
                ["Avatar_LeftForeArm"] = HumanBodyBones.LeftLowerArm,
                ["Avatar_LeftHand"] = HumanBodyBones.LeftHand,

                ["Avatar_LeftHandThumb1"] = HumanBodyBones.LeftThumbProximal,
                ["Avatar_LeftHandThumb2"] = HumanBodyBones.LeftThumbIntermediate,
                ["Avatar_LeftHandThumb3"] = HumanBodyBones.LeftThumbDistal,
                ["Avatar_LeftHandThumb4"] = HumanBodyBones.LeftThumbDistal,
                ["Avatar_LeftHandIndex1"] = HumanBodyBones.LeftIndexProximal,
                ["Avatar_LeftHandIndex2"] = HumanBodyBones.LeftIndexIntermediate,
                ["Avatar_LeftHandIndex3"] = HumanBodyBones.LeftIndexDistal,
                ["Avatar_LeftHandIndex4"] = HumanBodyBones.LeftIndexDistal,
                ["Avatar_LeftHandMiddle1"] = HumanBodyBones.LeftMiddleProximal,
                ["Avatar_LeftHandMiddle2"] = HumanBodyBones.LeftMiddleIntermediate,
                ["Avatar_LeftHandMiddle3"] = HumanBodyBones.LeftMiddleDistal,
                ["Avatar_LeftHandMiddle4"] = HumanBodyBones.LeftMiddleDistal,
                ["Avatar_LeftHandRing1"] = HumanBodyBones.LeftRingProximal,
                ["Avatar_LeftHandRing2"] = HumanBodyBones.LeftRingIntermediate,
                ["Avatar_LeftHandRing3"] = HumanBodyBones.LeftRingDistal,
                ["Avatar_LeftHandRing4"] = HumanBodyBones.LeftRingDistal,
                ["Avatar_LeftHandPinky1"] = HumanBodyBones.LeftLittleProximal,
                ["Avatar_LeftHandPinky2"] = HumanBodyBones.LeftLittleIntermediate,
                ["Avatar_LeftHandPinky3"] = HumanBodyBones.LeftLittleDistal,
                ["Avatar_LeftHandPinky4"] = HumanBodyBones.LeftLittleDistal,

                ["Avatar_RightShoulder"] = HumanBodyBones.RightShoulder,
                ["Avatar_RightArm"] = HumanBodyBones.RightUpperArm,
                ["Avatar_RightForeArm"] = HumanBodyBones.RightLowerArm,
                ["Avatar_RightHand"] = HumanBodyBones.RightHand,

                ["Avatar_RightHandThumb1"] = HumanBodyBones.RightThumbProximal,
                ["Avatar_RightHandThumb2"] = HumanBodyBones.RightThumbIntermediate,
                ["Avatar_RightHandThumb3"] = HumanBodyBones.RightThumbDistal,
                ["Avatar_RightHandThumb4"] = HumanBodyBones.RightThumbDistal,
                ["Avatar_RightHandIndex1"] = HumanBodyBones.RightIndexProximal,
                ["Avatar_RightHandIndex2"] = HumanBodyBones.RightIndexIntermediate,
                ["Avatar_RightHandIndex3"] = HumanBodyBones.RightIndexDistal,
                ["Avatar_RightHandIndex4"] = HumanBodyBones.RightIndexDistal,
                ["Avatar_RightHandMiddle1"] = HumanBodyBones.RightMiddleProximal,
                ["Avatar_RightHandMiddle2"] = HumanBodyBones.RightMiddleIntermediate,
                ["Avatar_RightHandMiddle3"] = HumanBodyBones.RightMiddleDistal,
                ["Avatar_RightHandMiddle4"] = HumanBodyBones.RightMiddleDistal,
                ["Avatar_RightHandRing1"] = HumanBodyBones.RightRingProximal,
                ["Avatar_RightHandRing2"] = HumanBodyBones.RightRingIntermediate,
                ["Avatar_RightHandRing3"] = HumanBodyBones.RightRingDistal,
                ["Avatar_RightHandRing4"] = HumanBodyBones.RightRingDistal,
                ["Avatar_RightHandPinky1"] = HumanBodyBones.RightLittleProximal,
                ["Avatar_RightHandPinky2"] = HumanBodyBones.RightLittleIntermediate,
                ["Avatar_RightHandPinky3"] = HumanBodyBones.RightLittleDistal,
                ["Avatar_RightHandPinky4"] = HumanBodyBones.RightLittleDistal,

                ["Avatar_LeftUpLeg"] = HumanBodyBones.LeftUpperLeg,
                ["Avatar_LeftLeg"] = HumanBodyBones.LeftLowerLeg,
                ["Avatar_LeftFoot"] = HumanBodyBones.LeftFoot,
                ["Avatar_LeftToeBase"] = HumanBodyBones.LeftToes,

                ["Avatar_RightUpLeg"] = HumanBodyBones.RightUpperLeg,
                ["Avatar_RightLeg"] = HumanBodyBones.RightLowerLeg,
                ["Avatar_RightFoot"] = HumanBodyBones.RightFoot,
                ["Avatar_RightToeBase"] = HumanBodyBones.RightToes,
            };
        }
    }
}
