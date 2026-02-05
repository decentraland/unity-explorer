using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.Ipfs;

namespace DCL.AvatarRendering.Emotes
{
    public interface ITrimmedEmote : IThumbnailAttachment
    {
        public int Amount { get; set; }
        TrimmedEmoteDTO TrimmedDTO { get; }

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
