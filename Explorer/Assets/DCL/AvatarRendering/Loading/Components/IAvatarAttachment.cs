using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.DTO;
using DCL.Diagnostics;
using DCL.Ipfs;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;

namespace DCL.AvatarRendering.Loading.Components
{
    public interface IAvatarAttachment : IThumbnailAttachment
    {
        bool IsLoading { get; }

        public void UpdateLoadingStatus(bool isLoading);

        /// <summary>
        ///     If null - promise has never been created, otherwise it could contain the result or be un-initialized
        /// </summary>
        StreamableLoadingResult<SpriteData>.WithFallback? ThumbnailAssetResult { get; set; }

        AvatarAttachmentDTO? DTO { get; }

        int Amount { get; }
        void SetAmount(int amount);
        
        public string ToString() =>
            $"AvatarAttachment({DTO.GetHash()} | {this.GetUrn()})";

        // IThumbnailAttachment implementation
        URLPath IThumbnailAttachment.GetThumbnail()
        {
            const string THUMBNAIL_DEFAULT_KEY = "thumbnail.png";
            string thumbnailHash = DTO.Metadata.thumbnail;

            if (thumbnailHash == THUMBNAIL_DEFAULT_KEY && DTO.content != null)
            {
                for (int i = 0; i < DTO.content.Length; i++)
                {
                    if (DTO.content[i].file == THUMBNAIL_DEFAULT_KEY)
                    {
                        thumbnailHash = DTO.content[i].hash;
                        break;
                    }
                }
            }

            return new URLPath(thumbnailHash!);
        }

        URN IThumbnailAttachment.GetUrn()
        {
            return DTO.Metadata.id;
        }

        string IThumbnailAttachment.GetHash()
        {
            return DTO.GetHash();
        }

        AssetBundleManifestVersion? IThumbnailAttachment.GetAssetBundleManifestVersion()
        {
            return DTO.assetBundleManifestVersion;
        }

        string? IThumbnailAttachment.GetContentDownloadUrl()
        {
            return DTO.ContentDownloadUrl;
        }

        string? IThumbnailAttachment.GetEntityId()
        {
            return DTO.id;
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

    public static class AvatarAttachmentExtensions
    {
        public static bool IsUnisex(this IAvatarAttachment avatarAttachment) =>
            avatarAttachment.DTO.Metadata.AbstractData.representations.Length > 1;

        public static URN GetUrn(this IAvatarAttachment avatarAttachment) =>
            avatarAttachment.DTO.Metadata.id;

        public static string GetCategory(this IAvatarAttachment avatarAttachment) =>
            avatarAttachment.DTO.Metadata.AbstractData.category;

        public static string GetDescription(this IAvatarAttachment avatarAttachment) =>
            avatarAttachment.DTO.Metadata.description;

        public static bool IsThirdParty(this IAvatarAttachment avatarAttachment) =>
            avatarAttachment.GetUrn().IsThirdPartyCollection();

        public static string GetName(this IAvatarAttachment avatarAttachment, string langCode = "en")
        {
            string result = avatarAttachment.DTO.Metadata.name;

            if (avatarAttachment.DTO.Metadata.i18n == null)
                return result;

            for (var i = 0; i < avatarAttachment.DTO.Metadata.i18n.Length; i++)
            {
                if (avatarAttachment.DTO.Metadata.i18n[i].code != langCode)
                    continue;

                result = avatarAttachment.DTO.Metadata.i18n[i].text;
                break;
            }

            return result;
        }

        public static string GetRarity(this IAvatarAttachment avatarAttachment)
        {
            const string DEFAULT_RARITY = "base";
            string result = avatarAttachment.DTO.Metadata?.rarity ?? DEFAULT_RARITY;
            return string.IsNullOrEmpty(result) ? DEFAULT_RARITY : result;
        }

        public static URLPath GetThumbnail(this IAvatarAttachment avatarAttachment)
        {
            const string THUMBNAIL_DEFAULT_KEY = "thumbnail.png";
            AvatarAttachmentDTO wearableDTO = avatarAttachment.DTO;

            string thumbnailHash = wearableDTO.Metadata.thumbnail;

            if (thumbnailHash == THUMBNAIL_DEFAULT_KEY && avatarAttachment.TryGetContentHashByKey(THUMBNAIL_DEFAULT_KEY, out string? hash))
                thumbnailHash = hash!;

            return new URLPath(thumbnailHash!);
        }

        public static bool TryGetMainFileHash(this IAvatarAttachment avatarAttachment, BodyShape bodyShape, out string? hash)
        {
            AvatarAttachmentDTO dto = avatarAttachment.DTO;

            if (dto.Metadata?.AbstractData?.representations == null)
            {
                hash = null;
                return false;
            }

            // The length of arrays is small, so O(N) complexity is fine
            // Avoid iterator allocations with "for" loop
            for (var i = 0; i < dto.Metadata.AbstractData.representations.Length; i++)
            {
                var representation = dto.Metadata.AbstractData.representations[i];

                for (var id = 0; id < representation.bodyShapes.Length; id++)
                    if (Equals(representation.bodyShapes[id], (string)bodyShape))
                        return avatarAttachment.TryGetContentHashByKey(representation.mainFile, out hash);
            }

            hash = null;
            return false;
        }

        public static bool TryGetContentHashByKey(this IAvatarAttachment avatarAttachment, string key, out string? hash)
        {
            AvatarAttachmentDTO wearableDTO = avatarAttachment.DTO;

            if (wearableDTO.content != null)
            {
                for (var i = 0; i < wearableDTO.content.Length; i++)
                    if (wearableDTO.content[i].file == key)
                    {
                        hash = wearableDTO.content[i].hash;
                        return true;
                    }
            }
            else
                ReportHub.LogError(ReportCategory.WEARABLE, $"No content found in DTO for wearable with ID: {avatarAttachment.DTO.Metadata.id}");

            hash = null;
            return false;
        }

    }
}
