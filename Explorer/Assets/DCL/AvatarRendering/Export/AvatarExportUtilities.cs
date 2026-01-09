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
            var metadata = wearable.DTO?.Metadata;
            string name;

            if (metadata == null)
                name = "null";
            else if (!string.IsNullOrEmpty(metadata.name))
                name = metadata.name;
            else if (!string.IsNullOrEmpty(metadata.id))
                name = metadata.id;
            else
                name = "null";

            return new WearableExportInfo
            {
                Name = name,
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

    public struct WearableExportInfo
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public string MarketPlaceUrl { get; set; }
    }
}
