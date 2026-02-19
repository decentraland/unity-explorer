using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.DTO;
using DCL.Ipfs;

namespace DCL.AvatarRendering.Loading.Components
{
    public interface ITrimmedAvatarAttachment : IThumbnailAttachment
    {
        TrimmedAvatarAttachmentDTO TrimmedDTO { get; }
        void SetAmount(int amount);

        string GetName()
        {
            if (string.IsNullOrEmpty(TrimmedDTO.Metadata.name))
                return "NAME_WEARABLE";

            return TrimmedDTO.Metadata.name;
        }

        public string GetCategory() =>
            TrimmedDTO.Metadata.AbstractData.category;

        public string GetRarity()
        {
            const string DEFAULT_RARITY = "base";
            string result = TrimmedDTO.Metadata?.rarity ?? DEFAULT_RARITY;
            return string.IsNullOrEmpty(result) ? DEFAULT_RARITY : result;
        }

        public new URN GetUrn() =>
            TrimmedDTO.Metadata.id;

        public new URLPath GetThumbnail() =>
            new (TrimmedDTO.thumbnail);

        public new string GetHash() =>
            TrimmedDTO.GetHash();

        public new AssetBundleManifestVersion? GetAssetBundleManifestVersion() =>
            TrimmedDTO.assetBundleManifestVersion;

        public new string? GetContentDownloadUrl() =>
            TrimmedDTO.ContentDownloadUrl;

        public new string? GetEntityId() =>
            TrimmedDTO.id;

        URN IThumbnailAttachment.GetUrn() => GetUrn();

        URLPath IThumbnailAttachment.GetThumbnail() => GetThumbnail();

        string IThumbnailAttachment.GetHash() => GetHash();

        AssetBundleManifestVersion? IThumbnailAttachment.GetAssetBundleManifestVersion() => GetAssetBundleManifestVersion();

        string? IThumbnailAttachment.GetContentDownloadUrl() => GetContentDownloadUrl();

        string? IThumbnailAttachment.GetEntityId() => GetEntityId();
    }
}
