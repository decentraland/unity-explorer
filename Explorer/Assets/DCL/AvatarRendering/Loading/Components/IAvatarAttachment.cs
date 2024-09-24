using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.DTO;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using SceneRunner.Scene;
using System.Linq;

namespace DCL.AvatarRendering.Loading.Components
{
    public interface IAvatarAttachment
    {
        bool IsLoading { get; }

        public void UpdateLoadingStatus(bool isLoading);

        /// <summary>
        ///     Might be never resolved if Wearable is loaded from the Embedded Source
        /// </summary>
        StreamableLoadingResult<SceneAssetBundleManifest>? ManifestResult { get; set; }

        /// <summary>
        ///     If null - promise has never been created, otherwise it could contain the result or be un-initialized
        /// </summary>
        StreamableLoadingResult<SpriteData>.WithFallback? ThumbnailAssetResult { get; set; }

        AvatarAttachmentDTO DTO { get; }

        public string ToString() =>
            $"AvatarAttachment({DTO.GetHash()} | {this.GetUrn()})";
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
            AvatarAttachmentDTO wearableDTO = avatarAttachment.DTO;

            // The length of arrays is small, so O(N) complexity is fine
            // Avoid iterator allocations with "for" loop
            for (var i = 0; i < wearableDTO.Metadata.AbstractData.representations.Length; i++)
            {
                var representation = wearableDTO.Metadata.AbstractData.representations[i];

                if (representation.bodyShapes.Contains(bodyShape))
                    return avatarAttachment.TryGetContentHashByKey(representation.mainFile, out hash);
            }

            hash = null;
            return false;
        }

        public static bool TryGetContentHashByKey(this IAvatarAttachment avatarAttachment, string key, out string? hash)
        {
            AvatarAttachmentDTO wearableDTO = avatarAttachment.DTO;

            for (var i = 0; i < wearableDTO.content.Length; i++)
                if (wearableDTO.content[i].file == key)
                {
                    hash = wearableDTO.content[i].hash;
                    return true;
                }

            hash = null;
            return false;
        }

        public static void UpdateManifest(this IAvatarAttachment avatarAttachment, StreamableLoadingResult<SceneAssetBundleManifest> result)
        {
            avatarAttachment.ManifestResult = result;
            avatarAttachment.UpdateLoadingStatus(false);
        }

        public static void ResetManifest(this IAvatarAttachment avatarAttachment)
        {
            avatarAttachment.ManifestResult = null;
            avatarAttachment.UpdateLoadingStatus(false);
        }
    }
}
