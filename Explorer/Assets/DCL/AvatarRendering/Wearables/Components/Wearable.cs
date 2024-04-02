using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

//Removed all references to EmoteData in WearableItem
namespace DCL.AvatarRendering.Wearables.Components
{
    public enum WearableType : byte
    {
        Regular,
        BodyShape,
        FacialFeature
    }

    [Serializable]
    public class Wearable : IWearable
    {
        private const string DEFAULT_RARITY = "base";
        public StreamableLoadingResult<SceneAssetBundleManifest>? ManifestResult { get; set; }
        // public StreamableLoadingResult<WearableAssetBase>?[] WearableAssetResults { get; private set; } = new StreamableLoadingResult<WearableAssetBase>?[BodyShape.COUNT];

        public WearableAssets[] WearableAssetResults { get; } = new WearableAssets[BodyShape.COUNT];

        public StreamableLoadingResult<WearableDTO> WearableDTO { get; private set; }
        public StreamableLoadingResult<Sprite>? WearableThumbnail { get; set; }

        public WearableType Type { get; private set; }

        public bool IsLoading { get; set; } = true;

        public Wearable()
        {
        }

        public Wearable(StreamableLoadingResult<WearableDTO> dto)
        {
            ResolveDTO(dto);
            IsLoading = false;
        }

        public void ResolveDTO(StreamableLoadingResult<WearableDTO> result)
        {
            Assert.IsTrue(!WearableDTO.IsInitialized || !WearableDTO.Succeeded);
            WearableDTO = result;

            if (!result.Succeeded) return;

            if (IsFacialFeature())
                Type = WearableType.FacialFeature;
            else if (IsBodyShape())
                Type = WearableType.BodyShape;
            else
                Type = WearableType.Regular;
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

        internal bool IsFacialFeature() =>
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

        internal bool IsBodyShape() =>
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
