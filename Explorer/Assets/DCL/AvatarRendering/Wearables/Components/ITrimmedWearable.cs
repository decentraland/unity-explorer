using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;

namespace DCL.AvatarRendering.Wearables.Components
{
    public interface ITrimmedWearable : IThumbnailAttachment
    {
        bool IsCompatibleWithBodyShape(string bodyShape);

        /// <summary>
        ///     If null - promise has never been created, otherwise it could contain the result or be un-initialized
        /// </summary>
        StreamableLoadingResult<SpriteData>.WithFallback? ThumbnailAssetResult { get; set; }

        TrimmedAvatarAttachmentDTO TrimmedDTO { get; }

        public URLPath GetThumbnail() =>
            new (TrimmedDTO.thumbnail);

        URN GetUrn() =>
            this.TrimmedDTO.Metadata.id;

        string GetRarity()
        {
            const string DEFAULT_RARITY = "base";
            string result = this.TrimmedDTO.Metadata?.rarity ?? DEFAULT_RARITY;
            return string.IsNullOrEmpty(result) ? DEFAULT_RARITY : result;
        }

        string GetCategory() =>
            this.TrimmedDTO.Metadata.AbstractData.category;

        // IThumbnailAttachment implementation
        URLPath IThumbnailAttachment.GetThumbnail() => this.GetThumbnail();
        URN IThumbnailAttachment.GetUrn() => this.GetUrn();
        string IThumbnailAttachment.GetHash() => TrimmedDTO.GetHash();
        DCL.Ipfs.AssetBundleManifestVersion? IThumbnailAttachment.GetAssetBundleManifestVersion() => TrimmedDTO.assetBundleManifestVersion;
        string? IThumbnailAttachment.GetContentDownloadUrl() => TrimmedDTO.ContentDownloadUrl;
        string? IThumbnailAttachment.GetEntityId() => TrimmedDTO.id;
    }
}
