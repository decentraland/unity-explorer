using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;

namespace DCL.AvatarRendering.Loading.Components
{
    public interface ITrimmedAvatarAttachment : IThumbnailAttachment
    {
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
