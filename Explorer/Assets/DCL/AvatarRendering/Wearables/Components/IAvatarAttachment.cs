using CommunicationData.URLHelpers;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Components
{
    public partial interface IAvatarAttachment
    {
        private const string DEFAULT_RARITY = "base";

        bool IsLoading { get; set; }

        /// <summary>
        ///     Might be never resolved if Wearable is loaded from the Embedded Source
        /// </summary>
        StreamableLoadingResult<SceneAssetBundleManifest>? ManifestResult { get; set; }

        StreamableLoadingResult<Sprite>? ThumbnailAssetResult { get; set; }

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

        bool IsCollectible()
        {
            var id = GetUrn().ToString();
            return !id.StartsWith("urn:decentraland:off-chain:base-avatars:");
        }

        AvatarAttachmentDTO GetDTO();

        public string ToString() =>
            $"AvatarAttachment({GetHash()} | {GetUrn()})";
    }
}
