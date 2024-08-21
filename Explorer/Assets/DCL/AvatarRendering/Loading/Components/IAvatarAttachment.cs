using CommunicationData.URLHelpers;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.AvatarRendering.Loading.Components
{
    public interface IAvatarAttachment
    {
        private const string THUMBNAIL_DEFAULT_KEY = "thumbnail.png";
        private const string DEFAULT_RARITY = "base";

        bool IsLoading { get; set; }

        /// <summary>
        ///     Might be never resolved if Wearable is loaded from the Embedded Source
        /// </summary>
        StreamableLoadingResult<SceneAssetBundleManifest>? ManifestResult { get; set; }

        StreamableLoadingResult<Sprite>? ThumbnailAssetResult { get; set; }

        AvatarAttachmentDTO GetDTO();

        string GetHash() =>
            GetDTO().id;

        URN GetUrn() =>
            GetDTO().Metadata.id;

        string GetName(string langCode = "en")
        {
            string result = GetDTO().Metadata.name;

            if (GetDTO().Metadata.i18n == null)
                return result;

            for (var i = 0; i < GetDTO().Metadata.i18n.Length; i++)
            {
                if (GetDTO().Metadata.i18n[i].code != langCode)
                    continue;

                result = GetDTO().Metadata.i18n[i].text;
                break;
            }

            return result;
        }

        string GetCategory() =>
            GetDTO().Metadata.AbstractData.category;

        string GetDescription() =>
            GetDTO().Metadata.description;

        string GetRarity()
        {
            string result = GetDTO().Metadata.rarity ?? DEFAULT_RARITY;

            if (string.IsNullOrEmpty(result))
                result = DEFAULT_RARITY;

            return result;
        }

        bool IsUnisex() =>
            GetDTO().Metadata.AbstractData.representations.Length > 1;

        bool IsThirdParty() =>
            GetUrn().IsThirdPartyCollection();

        URLPath GetThumbnail()
        {
            AvatarAttachmentDTO wearableDTO = GetDTO();

            string thumbnailHash = wearableDTO.Metadata.thumbnail;

            if (thumbnailHash == THUMBNAIL_DEFAULT_KEY && TryGetContentHashByKey(THUMBNAIL_DEFAULT_KEY, out string? hash))
                thumbnailHash = hash!;

            return new URLPath(thumbnailHash!);
        }

        bool TryGetMainFileHash(BodyShape bodyShape, out string? hash)
        {
            AvatarAttachmentDTO wearableDTO = GetDTO();

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
            AvatarAttachmentDTO wearableDTO = GetDTO();

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
            $"AvatarAttachment({GetHash()} | {GetUrn()})";
    }

    public partial interface IAvatarAttachment<TModelDTO> : IAvatarAttachment
    {
        StreamableLoadingResult<TModelDTO> Model { get; set; }

        bool IsOnChain();
    }
}
