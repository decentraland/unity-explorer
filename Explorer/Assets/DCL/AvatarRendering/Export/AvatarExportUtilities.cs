using DCL.AvatarRendering.Wearables.Components;
using System.Collections.Generic;
using DCL.Diagnostics;
using System.Text;
using UniHumanoid;
using UnityEngine;
using VRM;

namespace DCL.AvatarRendering.Export
{
    public static class AvatarExportUtilities
    {
        public static void EnforceDCLTPose(Dictionary<HumanBodyBones, Transform> bones)
        {
            // Reset all to identity first
            foreach (var bone in bones.Values)
                bone.localRotation = Quaternion.identity;

            // DCL-specific corrections
            if (bones.TryGetValue(HumanBodyBones.Hips, out var hips))
                hips.localRotation = Quaternion.Euler(-90, 0, -180);

            if (bones.TryGetValue(HumanBodyBones.Spine, out var spine))
                spine.localRotation = Quaternion.Euler(0, 0, -180);

            if (bones.TryGetValue(HumanBodyBones.LeftShoulder, out var leftShoulder))
                leftShoulder.localRotation = Quaternion.Euler(0, -180, -90);

            if (bones.TryGetValue(HumanBodyBones.RightShoulder, out var rightShoulder))
                rightShoulder.localRotation = Quaternion.Euler(0, 0, -90);

            if (bones.TryGetValue(HumanBodyBones.LeftFoot, out var leftFoot))
                leftFoot.localRotation = Quaternion.Euler(-90, 180, 0);

            if (bones.TryGetValue(HumanBodyBones.RightFoot, out var rightFoot))
                rightFoot.localRotation = Quaternion.Euler(-90, 180, 0);

            ReportHub.Log(ReportCategory.AVATAR_EXPORT, "Applied T-pose");
        }

        public static Mesh CloneAndRecalculateBounds(Mesh source)
        {
            var mesh = Object.Instantiate(source);
            mesh.name = source.name;
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Avatar CreateHumanoidAvatar(GameObject root,
            Dictionary<HumanBodyBones, Transform> humanBones)
        {
            if (!root.TryGetComponent(out Animator animator))
                animator = root.AddComponent<Animator>();

            var avatarDescription = AvatarDescription.Create();
            avatarDescription.SetHumanBones(humanBones);

            var avatar = avatarDescription.CreateAvatarAndSetup(root.transform);

            if (avatar.isValid)
            {
                avatar.name = "DCL_Export_Avatar";
                animator.avatar = avatar;
            }
            else
            {
                ReportHub.LogError(ReportCategory.AVATAR_EXPORT, "Avatar is not valid!");
                return null;
            }

            return avatar;
        }

        public static WearableExportInfo CreateWearableInfo(IWearable wearable)
        {
            var dto = wearable.DTO;
            return new WearableExportInfo
            {
                Name = !string.IsNullOrEmpty(dto.Metadata.name) ? dto.Metadata.name : dto.Metadata.id,
                Category = wearable.GetCategory(),
                MarketPlaceUrl = wearable.GetMarketplaceLink()
            };
        }

        public static VRMMetaObject CreateVrmMetaObject(string authorName,
            List<WearableExportInfo> wearableInfos)
        {
            var meta = ScriptableObject.CreateInstance<VRMMetaObject>();

            meta.Title = $"{authorName} avatar";
            meta.Reference = BuildWearableReferences(wearableInfos);

            meta.Author = "Decentraland";
            meta.ContactInformation = "info@decentraland.org";
            meta.AllowedUser = AllowedUser.Everyone;
            meta.ViolentUssage = UssageLicense.Disallow;
            meta.SexualUssage = UssageLicense.Disallow;
            meta.CommercialUssage = UssageLicense.Disallow;
            meta.LicenseType = LicenseType.Redistribution_Prohibited;
            meta.Version = "1.0, UniVRM v0.112.0";

            return meta;
        }

        private static string BuildWearableReferences(List<WearableExportInfo> wearables)
        {
            if (wearables == null || wearables.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();

            foreach (var wearable in wearables)
            {
                if (sb.Length > 0)
                    sb.AppendLine(" | ");

                sb.Append(wearable.Category).Append(": ").Append(wearable.Name);

                if (!string.IsNullOrEmpty(wearable.MarketPlaceUrl))
                    sb.Append(": ").Append(wearable.MarketPlaceUrl);
            }

            return sb.ToString();
        }
    }
}
