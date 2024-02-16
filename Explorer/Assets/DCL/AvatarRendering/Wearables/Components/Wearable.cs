using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

//Removed all references to EmoteData in WearableItem
namespace DCL.AvatarRendering.Wearables.Components
{
    [Serializable]
    public class Wearable : IWearable
    {
        private const string THUMBNAIL_DEFAULT_KEY = "thumbnail.png";
        private const string DEFAULT_RARITY = "base";
        public StreamableLoadingResult<SceneAssetBundleManifest>? ManifestResult { get; set; }
        public StreamableLoadingResult<WearableAsset>?[] WearableAssetResults { get; private set; } = new StreamableLoadingResult<WearableAsset>?[BodyShape.COUNT];
        public StreamableLoadingResult<WearableDTO> WearableDTO { get; set; }

        public StreamableLoadingResult<Sprite>? WearableThumbnail { get; set; }
        public bool IsLoading { get; set; } = true;

        public URLPath GetThumbnail()
        {
            string thumbnailHash = WearableDTO.Asset.metadata.thumbnail;

            if (thumbnailHash == THUMBNAIL_DEFAULT_KEY)
                thumbnailHash = GetContentHashByKey(THUMBNAIL_DEFAULT_KEY);

            return new URLPath(thumbnailHash);
        }

        private string GetContentHashByKey(string key)
        {
            for (var i = 0; i < WearableDTO.Asset.content.Length; i++)
            {
                if (WearableDTO.Asset.content[i].file == key)
                    return WearableDTO.Asset.content[i].hash;
            }

            return "";
        }

        public string GetMainFileHash(BodyShape bodyShape)
        {
            var mainFileKey = "";

            // The length of arrays is small, so O(N) complexity is fine
            // Avoid iterator allocations with "for" loop
            for (var i = 0; i < WearableDTO.Asset.metadata.data.representations.Length; i++)
            {
                WearableDTO.WearableMetadataDto.Representation representation = WearableDTO.Asset.metadata.data.representations[i];

                if (representation.bodyShapes.Contains(bodyShape))
                {
                    mainFileKey = representation.mainFile;
                    break;
                }
            }

            return GetContentHashByKey(mainFileKey);
        }

        public string GetHash() =>
            WearableDTO.Asset.id;

        public URN GetUrn() =>
            WearableDTO.Asset.metadata.id;

        public string GetName() =>
            WearableDTO.Asset.metadata.name;

        public string GetCategory() =>
            WearableDTO.Asset.metadata.data.category;

        public string GetDescription() =>
            WearableDTO.Asset.metadata.description;

        public string GetCreator() =>
            "";

        public string GetRarity() =>
            WearableDTO.Asset.metadata.rarity ?? DEFAULT_RARITY;

        public bool IsUnisex() =>
            WearableDTO.Asset.metadata.data.representations.Length > 1;

        public void GetHidingList(string bodyShapeType, HashSet<string> hideListResult)
        {
            WearableDTO.WearableMetadataDto.Representation representation = GetRepresentation(bodyShapeType);
            WearableDTO.WearableMetadataDto.DataDto data = WearableDTO.Asset.metadata.data;

            if (representation?.overrideHides == null || representation.overrideHides.Length == 0)
                hideListResult.UnionWith(data.hides ?? Enumerable.Empty<string>());
            else
                hideListResult.UnionWith(representation.overrideHides);

            if (IsSkin())
                hideListResult.UnionWith(WearablesConstants.SKIN_IMPLICIT_CATEGORIES);

            // we apply this rule to hide the hands by default if the wearable is an upper body or hides the upper body
            bool isOrHidesUpperBody = hideListResult.Contains(WearablesConstants.Categories.UPPER_BODY) || data.category == WearablesConstants.Categories.UPPER_BODY;

            // the rule is ignored if the wearable contains the removal of this default rule (newer upper bodies since the release of hands)
            bool removesHandDefault = data.removesDefaultHiding?.Contains(WearablesConstants.Categories.HANDS) ?? false;

            // why we do this? because old upper bodies contains the base hand mesh, and they might clip with the new handwear items
            if (isOrHidesUpperBody && !removesHandDefault)
                hideListResult.UnionWith(WearablesConstants.UPPER_BODY_DEFAULT_HIDES);

            string[] replaces = GetReplacesList(bodyShapeType);

            if (replaces != null)
                hideListResult.UnionWith(replaces);

            // Safeguard so no wearable can hide itself
            hideListResult.Remove(data.category);
        }

        public WearableDTO.WearableMetadataDto.DataDto GetData() =>
            WearableDTO.Asset.metadata.data;

        public bool isFacialFeature() =>
            WearablesConstants.FACIAL_FEATURES.Contains(GetCategory());

        public bool IsCompatibleWithBodyShape(string bodyShape)
        {
            foreach (WearableDTO.WearableMetadataDto.Representation dataRepresentation in WearableDTO.Asset.metadata.data.representations)
            {
                if (dataRepresentation.bodyShapes.Contains(bodyShape))
                    return true;
            }

            return false;
        }

        public bool IsBodyShape() =>
            GetCategory().Equals(WearablesConstants.Categories.BODY_SHAPE);

        private bool IsSkin() =>
            GetCategory() == WearablesConstants.Categories.SKIN;

        private WearableDTO.WearableMetadataDto.Representation GetRepresentation(string bodyShapeType)
        {
            foreach (WearableDTO.WearableMetadataDto.Representation representation in WearableDTO.Asset.metadata.data.representations)
            {
                if (representation.bodyShapes.Contains(bodyShapeType))
                    return representation;
            }

            return null;
        }

        public string[] GetReplacesList(string bodyShapeType)
        {
            WearableDTO.WearableMetadataDto.Representation representation = GetRepresentation(bodyShapeType);

            if (representation?.overrideReplaces == null || representation.overrideReplaces.Length == 0)
                return WearableDTO.Asset.metadata.data.replaces;

            return representation.overrideReplaces;
        }
    }
}
