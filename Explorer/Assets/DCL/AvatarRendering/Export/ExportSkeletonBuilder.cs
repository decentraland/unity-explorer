using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.Export
{
    public static class ExportSkeletonBuilder
    {
        public static void MapBonesRecursive(
            Dictionary<HumanBodyBones, Transform> mapping, Transform bone)
        {
            if (BONE_DEFINITIONS.TryGetValue(bone.name, out var humanBone))
                mapping.Add(humanBone, bone);

            foreach (Transform child in bone)
                MapBonesRecursive(mapping, child);
        }

        private static readonly Dictionary<string, HumanBodyBones> BONE_DEFINITIONS = new ()
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
            ["Avatar_LeftHandIndex1"] = HumanBodyBones.LeftIndexProximal,
            ["Avatar_LeftHandIndex2"] = HumanBodyBones.LeftIndexIntermediate,
            ["Avatar_LeftHandIndex3"] = HumanBodyBones.LeftIndexDistal,
            ["Avatar_LeftHandMiddle1"] = HumanBodyBones.LeftMiddleProximal,
            ["Avatar_LeftHandMiddle2"] = HumanBodyBones.LeftMiddleIntermediate,
            ["Avatar_LeftHandMiddle3"] = HumanBodyBones.LeftMiddleDistal,
            ["Avatar_LeftHandRing1"] = HumanBodyBones.LeftRingProximal,
            ["Avatar_LeftHandRing2"] = HumanBodyBones.LeftRingIntermediate,
            ["Avatar_LeftHandRing3"] = HumanBodyBones.LeftRingDistal,
            ["Avatar_LeftHandPinky1"] = HumanBodyBones.LeftLittleProximal,
            ["Avatar_LeftHandPinky2"] = HumanBodyBones.LeftLittleIntermediate,
            ["Avatar_LeftHandPinky3"] = HumanBodyBones.LeftLittleDistal,

            ["Avatar_RightShoulder"] = HumanBodyBones.RightShoulder,
            ["Avatar_RightArm"] = HumanBodyBones.RightUpperArm,
            ["Avatar_RightForeArm"] = HumanBodyBones.RightLowerArm,
            ["Avatar_RightHand"] = HumanBodyBones.RightHand,

            ["Avatar_RightHandThumb1"] = HumanBodyBones.RightThumbProximal,
            ["Avatar_RightHandThumb2"] = HumanBodyBones.RightThumbIntermediate,
            ["Avatar_RightHandThumb3"] = HumanBodyBones.RightThumbDistal,
            ["Avatar_RightHandIndex1"] = HumanBodyBones.RightIndexProximal,
            ["Avatar_RightHandIndex2"] = HumanBodyBones.RightIndexIntermediate,
            ["Avatar_RightHandIndex3"] = HumanBodyBones.RightIndexDistal,
            ["Avatar_RightHandMiddle1"] = HumanBodyBones.RightMiddleProximal,
            ["Avatar_RightHandMiddle2"] = HumanBodyBones.RightMiddleIntermediate,
            ["Avatar_RightHandMiddle3"] = HumanBodyBones.RightMiddleDistal,
            ["Avatar_RightHandRing1"] = HumanBodyBones.RightRingProximal,
            ["Avatar_RightHandRing2"] = HumanBodyBones.RightRingIntermediate,
            ["Avatar_RightHandRing3"] = HumanBodyBones.RightRingDistal,
            ["Avatar_RightHandPinky1"] = HumanBodyBones.RightLittleProximal,
            ["Avatar_RightHandPinky2"] = HumanBodyBones.RightLittleIntermediate,
            ["Avatar_RightHandPinky3"] = HumanBodyBones.RightLittleDistal,

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
