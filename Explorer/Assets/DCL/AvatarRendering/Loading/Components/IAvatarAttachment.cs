using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.DTO;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System.Linq;
using UnityEngine;

namespace DCL.AvatarRendering.Loading.Components
{
    public interface IAvatarAttachment
    {
        private const string THUMBNAIL_DEFAULT_KEY = "thumbnail.png";
        private const string DEFAULT_RARITY = "base";

        bool IsLoading { get; }

        public void UpdateLoadingStatus(bool isLoading);

        /// <summary>
        ///     Might be never resolved if Wearable is loaded from the Embedded Source
        /// </summary>
        StreamableLoadingResult<SceneAssetBundleManifest>? ManifestResult { get; set; }

        StreamableLoadingResult<Sprite>? ThumbnailAssetResult { get; set; }

        AvatarAttachmentDTO DTO { get; }

        URN GetUrn() =>
            DTO.Metadata.id;

        string GetName(string langCode = "en")
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

        string GetCategory() =>
            DTO.Metadata.AbstractData.category;

        string GetDescription() =>
            DTO.Metadata.description;

        string GetRarity()
        {
            string result = DTO.Metadata.rarity ?? DEFAULT_RARITY;

            if (string.IsNullOrEmpty(result))
                result = DEFAULT_RARITY;

            return result;
        }

        bool IsUnisex() =>
            DTO.Metadata.AbstractData.representations.Length > 1;

        bool IsThirdParty() =>
            GetUrn().IsThirdPartyCollection();

        URLPath GetThumbnail()
        {
            AvatarAttachmentDTO wearableDTO = DTO;

            string thumbnailHash = wearableDTO.Metadata.thumbnail;

            if (thumbnailHash == THUMBNAIL_DEFAULT_KEY && TryGetContentHashByKey(THUMBNAIL_DEFAULT_KEY, out string? hash))
                thumbnailHash = hash!;

            return new URLPath(thumbnailHash!);
        }

        bool TryGetMainFileHash(BodyShape bodyShape, out string? hash)
        {
            AvatarAttachmentDTO wearableDTO = DTO;

            // The length of arrays is small, so O(N) complexity is fine
            // Avoid iterator allocations with "for" loop
            for (var i = 0; i < wearableDTO.Metadata.AbstractData.representations.Length; i++)
            {
                var representation = wearableDTO.Metadata.AbstractData.representations[i];

                if (representation.bodyShapes.Contains(bodyShape))
                    return TryGetContentHashByKey(representation.mainFile, out hash);
            }

            hash = null;
            return false;
        }

        bool TryGetContentHashByKey(string key, out string? hash)
        {
            AvatarAttachmentDTO wearableDTO = DTO;

            for (var i = 0; i < wearableDTO.content.Length; i++)
                if (wearableDTO.content[i].file == key)
                {
                    hash = wearableDTO.content[i].hash;
                    return true;
                }

            hash = null;
            return false;
        }

        public string ToString() =>
            $"AvatarAttachment({DTO.GetHash()} | {GetUrn()})";
    }

    public partial interface IAvatarAttachment<TModelDTO> : IAvatarAttachment
    {
        StreamableLoadingResult<TModelDTO> Model { get; set; }

        bool IsOnChain();

        public void ResolvedFailedDTO(StreamableLoadingResult<TModelDTO> result)
        {
            Model = result;
            UpdateLoadingStatus(false);
        }

        public void FinalizeLoading()
        {
            UpdateLoadingStatus(false);
        }

        public void ApplyAndMarkAsLoaded(TModelDTO modelDTO)
        {
            Model = new StreamableLoadingResult<TModelDTO>(modelDTO);
            UpdateLoadingStatus(false);
        }
    }
}
