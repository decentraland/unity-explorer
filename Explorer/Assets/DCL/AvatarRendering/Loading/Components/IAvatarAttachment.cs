using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.DTO;
using DCL.Diagnostics;
using DCL.Ipfs;
using ECS.StreamableLoading.Common.Components;

namespace DCL.AvatarRendering.Loading.Components
{
    public interface IAvatarAttachment : IThumbnailAttachment
    {
        bool IsLoading { get; }

        public void UpdateLoadingStatus(bool isLoading);

        AvatarAttachmentDTO? DTO { get; }

        int Amount { get; }
        void SetAmount(int amount);

        // IThumbnailAttachment implementation

        public new string GetHash() =>
            DTO.GetHash();

        public new AssetBundleManifestVersion? GetAssetBundleManifestVersion() =>
            DTO.assetBundleManifestVersion;

        public new string? GetContentDownloadUrl() =>
            DTO.ContentDownloadUrl;

        public new string? GetEntityId() =>
            DTO.id;

        public bool IsUnisex() =>
            DTO.Metadata.AbstractData.representations.Length > 1;

        public new URN GetUrn() =>
            DTO.Metadata.id;

        string IThumbnailAttachment.GetHash() => GetHash();

        AssetBundleManifestVersion? IThumbnailAttachment.GetAssetBundleManifestVersion() => GetAssetBundleManifestVersion();

        string? IThumbnailAttachment.GetContentDownloadUrl() => GetContentDownloadUrl();

        string? IThumbnailAttachment.GetEntityId() => GetEntityId();

        URN IThumbnailAttachment.GetUrn() => GetUrn();

        public string GetCategory() =>
            DTO.Metadata.AbstractData.category;

        public string GetDescription() =>
            DTO.Metadata.description;

        public bool IsThirdParty() =>
            GetUrn().IsThirdPartyCollection();

        public string GetName(string langCode = "en")
        {
            string result = DTO.Metadata.name;

            if (DTO.Metadata.i18n == null)
                return result;

            for (var i = 0; i < DTO.Metadata.i18n.Length; i++)
            {
                if (DTO.Metadata.i18n[i].code != langCode)
                    continue;

                result = DTO.Metadata.i18n[i].text;
                break;
            }

            return result;
        }

        public string GetRarity()
        {
            const string DEFAULT_RARITY = "base";
            string result = DTO.Metadata?.rarity ?? DEFAULT_RARITY;
            return string.IsNullOrEmpty(result) ? DEFAULT_RARITY : result;
        }

        public new URLPath GetThumbnail()
        {
            const string THUMBNAIL_DEFAULT_KEY = "thumbnail.png";

            string thumbnailHash = DTO.Metadata.thumbnail;

            if (thumbnailHash == THUMBNAIL_DEFAULT_KEY && TryGetContentHashByKey(THUMBNAIL_DEFAULT_KEY, out string? hash))
                thumbnailHash = hash!;

            return new URLPath(thumbnailHash!);
        }

        URLPath IThumbnailAttachment.GetThumbnail() => GetThumbnail();

        public bool TryGetMainFileHash(BodyShape bodyShape, out string? hash)
        {
            if (DTO.Metadata?.AbstractData?.representations == null)
            {
                hash = null;
                return false;
            }

            // The length of arrays is small, so O(N) complexity is fine
            // Avoid iterator allocations with "for" loop
            for (var i = 0; i < DTO.Metadata.AbstractData.representations.Length; i++)
            {
                var representation = DTO.Metadata.AbstractData.representations[i];

                for (var id = 0; id < representation.bodyShapes.Length; id++)
                    if (Equals(representation.bodyShapes[id], (string)bodyShape))
                        return TryGetContentHashByKey(representation.mainFile, out hash);
            }

            hash = null;
            return false;
        }

        public bool TryGetContentHashByKey(string key, out string? hash)
        {
            if (DTO.content != null)
            {
                for (var i = 0; i < DTO.content.Length; i++)
                    if (DTO.content[i].file == key)
                    {
                        hash = DTO.content[i].hash;
                        return true;
                    }
            }
            else
                ReportHub.LogError(ReportCategory.WEARABLE, $"No content found in DTO for wearable with ID: {DTO.Metadata.id}");

            hash = null;
            return false;
        }
    }

    public interface IAvatarAttachment<TModelDTO> : IAvatarAttachment
    {
        StreamableLoadingResult<TModelDTO> Model { get; set; }

        bool IsOnChain();

        public void ResolvedFailedDTO(StreamableLoadingResult<TModelDTO> result)
        {
            Model = result;
            UpdateLoadingStatus(false);
        }

        public void ApplyAndMarkAsLoaded(TModelDTO modelDTO)
        {
            Model = new StreamableLoadingResult<TModelDTO>(modelDTO);
            UpdateLoadingStatus(false);
        }
    }
}
