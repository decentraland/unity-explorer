using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Linq;

//Removed all references to EmoteData in WearableItem
namespace DCL.AvatarRendering.Wearables.Components
{
    [Serializable]
    public class Wearable : IWearable
    {
        public StreamableLoadingResult<SceneAssetBundleManifest>? ManifestResult { get; set; }
        public StreamableLoadingResult<WearableAsset>?[] WearableAssets { get; set; } = new StreamableLoadingResult<WearableAsset>?[BodyShape.COUNT];
        public StreamableLoadingResult<WearableDTO> WearableDTO { get; set; }
        public bool IsLoading { get; set; } = true;

        public string GetMainFileHash(BodyShape bodyShape)
        {
            var mainFileKey = "";
            var hashToReturn = "";

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

            for (var i = 0; i < WearableDTO.Asset.content.Length; i++)
            {
                WearableDTO.WearableContentDto wearableContentDto = WearableDTO.Asset.content[i];

                if (wearableContentDto.file.Equals(mainFileKey))
                {
                    hashToReturn = wearableContentDto.hash;
                    break;
                }
            }

            return hashToReturn;
        }

        public string GetHash() =>
            WearableDTO.Asset.id;

        public string GetUrn() =>
            WearableDTO.Asset.metadata.id;

        public string GetCategory() =>
            WearableDTO.Asset.metadata.data.category;

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

        //TODO: Implement Dispose method
        public void Dispose() { }

    }
}
