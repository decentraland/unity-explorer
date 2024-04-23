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

        string GetName() =>
            GetDTO().Metadata.name;

        string GetCategory() =>
            GetDTO().Metadata.AbstractData.category;

        string GetDescription() =>
            GetDTO().Metadata.description;

        string GetRarity() =>
            GetDTO().Metadata.rarity ?? DEFAULT_RARITY;

        bool IsUnisex() =>
            GetDTO().Metadata.AbstractData.representations.Length > 1;

        AvatarAttachmentDTO GetDTO();

        public string ToString() =>
            $"AvatarAttachment({GetHash()} | {GetUrn()})";
    }
}
