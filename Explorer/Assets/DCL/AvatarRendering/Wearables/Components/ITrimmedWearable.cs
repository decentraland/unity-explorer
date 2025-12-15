using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.Ipfs;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;

namespace DCL.AvatarRendering.Wearables.Components
{
    public interface ITrimmedWearable : IThumbnailAttachment
    {
        public int Amount { get; set; }
        bool IsCompatibleWithBodyShape(string bodyShape);

        /// <summary>
        ///     If null - promise has never been created, otherwise it could contain the result or be un-initialized
        /// </summary>
        StreamableLoadingResult<SpriteData>.WithFallback? ThumbnailAssetResult { get; set; }

        TrimmedAvatarAttachmentDTO TrimmedDTO { get; }

        public URLPath GetThumbnail()
        {
            return new URLPath (TrimmedDTO.thumbnail);
        }

        public bool IsSmart()
        {
            return TrimmedDTO.Metadata.isSmart;
        }

        URN GetUrn()
        {
            return TrimmedDTO.Metadata.id;
        }

        string GetRarity()
        {
            const string DEFAULT_RARITY = "base";
            string result = TrimmedDTO.Metadata?.rarity ?? DEFAULT_RARITY;
            return string.IsNullOrEmpty(result) ? DEFAULT_RARITY : result;
        }

        string GetCategory()
        {
            return TrimmedDTO.Metadata.AbstractData.category;
        }

        // IThumbnailAttachment implementation
        URLPath IThumbnailAttachment.GetThumbnail()
        {
            return GetThumbnail();
        }

        URN IThumbnailAttachment.GetUrn()
        {
            return GetUrn();
        }

        string IThumbnailAttachment.GetHash()
        {
            return TrimmedDTO.GetHash();
        }

        string GetName()
        {
            if (string.IsNullOrEmpty(TrimmedDTO.Metadata.name))
                return "NAME_WEARABLE";
            
            return TrimmedDTO.Metadata.name;
        }

        public void SetAmount(int amount)
        {
            Amount = amount;
        }

        AssetBundleManifestVersion? IThumbnailAttachment.GetAssetBundleManifestVersion()
        {
            return TrimmedDTO.assetBundleManifestVersion;
        }

        string? IThumbnailAttachment.GetContentDownloadUrl()
        {
            return TrimmedDTO.ContentDownloadUrl;
        }

        string? IThumbnailAttachment.GetEntityId()
        {
            return TrimmedDTO.id;
        }
    }
}