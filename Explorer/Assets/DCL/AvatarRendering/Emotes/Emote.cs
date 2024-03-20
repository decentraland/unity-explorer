using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.AvatarRendering.Emotes
{
    public class Emote : IEmote
    {
        private const string THUMBNAIL_DEFAULT_KEY = "thumbnail.png";
        private const string DEFAULT_RARITY = "base";

        public StreamableLoadingResult<SceneAssetBundleManifest>? ManifestResult { get; set; }
        public StreamableLoadingResult<WearableAsset>?[] WearableAssetResults { get; } = new StreamableLoadingResult<WearableAsset>?[BodyShape.COUNT];
        public StreamableLoadingResult<Sprite>? ThumbnailAssetResult { get; set; }
        public StreamableLoadingResult<EmoteDTO> Model { get; set; }
        public StreamableLoadingResult<AudioClip>? AudioAssetResult { get; set; }
        public bool IsLoading { get; set; } = true;

        public URLPath GetThumbnail()
        {
            string thumbnailHash = Model.Asset.metadata.thumbnail;

            if (thumbnailHash == THUMBNAIL_DEFAULT_KEY)
                thumbnailHash = GetContentHashByKey(THUMBNAIL_DEFAULT_KEY);

            return new URLPath(thumbnailHash);
        }

        private string GetContentHashByKey(string key)
        {
            for (var i = 0; i < Model.Asset.content.Length; i++)
                if (Model.Asset.content[i].file == key)
                    return Model.Asset.content[i].hash;

            return "";
        }

        public string GetMainFileHash(BodyShape bodyShape)
        {
            var mainFileKey = "";

            // The length of arrays is small, so O(N) complexity is fine
            // Avoid iterator allocations with "for" loop
            for (var i = 0; i < Model.Asset.metadata.emoteDataADR74.representations.Length; i++)
            {
                EmoteDTO.Metadata.Representation representation = Model.Asset.metadata.emoteDataADR74.representations[i];

                if (representation.bodyShapes.Contains(bodyShape))
                {
                    mainFileKey = representation.mainFile;
                    break;
                }
            }

            return GetContentHashByKey(mainFileKey);
        }

        public override string ToString() =>
            $"Emote({GetHash()} | {GetUrn()})";

        public string GetHash() =>
            Model.Asset.id;

        public URN GetUrn() =>
            Model.Asset.metadata.id;

        public string GetName() =>
            Model.Asset.metadata.name;

        public string GetCategory() =>
            Model.Asset.metadata.emoteDataADR74.category;

        public string GetDescription() =>
            Model.Asset.metadata.description;

        public string GetRarity() =>
            Model.Asset.metadata.rarity ?? DEFAULT_RARITY;

        public bool IsUnisex() =>
            Model.Asset.metadata.emoteDataADR74.representations.Length > 1;

        public bool IsLooping() =>
            Model.Asset.metadata.emoteDataADR74.loop;

        public void GetHidingList(string bodyShapeType, HashSet<string> hideListResult)
        {
            EmoteDTO.Metadata.Representation? representation = GetRepresentation(bodyShapeType);
            EmoteDTO.Metadata.Data data = Model.Asset.metadata.emoteDataADR74;

            if (representation.HasValue)
            {
                if (representation.Value.overrideHides == null || representation.Value.overrideHides.Length == 0)
                    hideListResult.UnionWith(data.hides ?? Enumerable.Empty<string>());
                else
                    hideListResult.UnionWith(representation.Value.overrideHides);
            }

            if (IsSkin())
                hideListResult.UnionWith(WearablesConstants.SKIN_IMPLICIT_CATEGORIES);

            // we apply this rule to hide the hands by default if the wearable is an upper body or hides the upper body
            bool isOrHidesUpperBody = hideListResult.Contains(WearablesConstants.Categories.UPPER_BODY) || data.category == WearablesConstants.Categories.UPPER_BODY;

            // the rule is ignored if the wearable contains the removal of this default rule (newer upper bodies since the release of hands)
            bool removesHandDefault = data.removesDefaultHiding?.Contains(WearablesConstants.Categories.HANDS) ?? false;

            // why we do this? because old upper bodies contains the base hand mesh, and they might clip with the new handwear items
            if (isOrHidesUpperBody && !removesHandDefault)
                hideListResult.UnionWith(WearablesConstants.UPPER_BODY_DEFAULT_HIDES);

            string[]? replaces = GetReplacesList(bodyShapeType);

            if (replaces != null)
                hideListResult.UnionWith(replaces);

            // Safeguard so no wearable can hide itself
            hideListResult.Remove(data.category);
        }

        public bool IsFacialFeature() =>
            WearablesConstants.FACIAL_FEATURES.Contains(GetCategory());

        public bool IsCompatibleWithBodyShape(string bodyShape)
        {
            foreach (EmoteDTO.Metadata.Representation dataRepresentation in Model.Asset.metadata.emoteDataADR74.representations)
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

        public string[]? GetReplacesList(string bodyShapeType)
        {
            EmoteDTO.Metadata.Representation? representation = GetRepresentation(bodyShapeType);

            if (representation?.overrideReplaces == null || representation?.overrideReplaces.Length == 0)
                return Model.Asset.metadata.emoteDataADR74.replaces;

            return representation?.overrideReplaces;
        }

        private EmoteDTO.Metadata.Representation? GetRepresentation(string bodyShapeType)
        {
            foreach (EmoteDTO.Metadata.Representation representation in Model.Asset.metadata.emoteDataADR74.representations)
                if (representation.bodyShapes.Contains(bodyShapeType))
                    return representation;

            return null;
        }
    }
}
