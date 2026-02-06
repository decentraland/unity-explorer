using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.Ipfs;

namespace DCL.AvatarRendering.Emotes
{
    public interface ITrimmedEmote : IThumbnailAttachment
    {
        public int Amount { get; set; }
        TrimmedEmoteDTO TrimmedDTO { get; }

        string GetName()
        {
            if (string.IsNullOrEmpty(TrimmedDTO.Metadata.name))
                return "NAME_WEARABLE";

            return TrimmedDTO.Metadata.name;
        }

        string GetCategory() =>
            TrimmedDTO.Metadata.AbstractData.category;

        string GetRarity()
        {
            const string DEFAULT_RARITY = "base";
            string result = TrimmedDTO.Metadata?.rarity ?? DEFAULT_RARITY;
            return string.IsNullOrEmpty(result) ? DEFAULT_RARITY : result;
        }

        // IThumbnailAttachment implementation
        URLPath IThumbnailAttachment.GetThumbnail() =>
            new (TrimmedDTO.thumbnail);

        URN IThumbnailAttachment.GetUrn() =>
            TrimmedDTO.Metadata.id;

        string IThumbnailAttachment.GetHash() =>
            TrimmedDTO.id;

        AssetBundleManifestVersion? IThumbnailAttachment.GetAssetBundleManifestVersion() =>
            TrimmedDTO.assetBundleManifestVersion;

        string? IThumbnailAttachment.GetContentDownloadUrl() =>
            TrimmedDTO.ContentDownloadUrl;

        string? IThumbnailAttachment.GetEntityId() =>
            TrimmedDTO.id;
    }
}
