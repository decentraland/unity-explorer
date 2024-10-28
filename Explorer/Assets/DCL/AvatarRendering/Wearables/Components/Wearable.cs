using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

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
        public StreamableLoadingResult<SceneAssetBundleManifest>? ManifestResult { get; set; }

        public WearableAssets[] WearableAssetResults { get; } = new WearableAssets[BodyShape.COUNT];

        public StreamableLoadingResult<WearableDTO> Model { get; set; }

        public StreamableLoadingResult<SpriteData>.WithFallback? ThumbnailAssetResult { get; set; }

        public WearableType Type { get; private set; }

        public bool IsLoading { get; private set; } = true;

        public void UpdateLoadingStatus(bool isLoading)
        {
            IsLoading = isLoading;
        }

        public Wearable() { }

        public Wearable(StreamableLoadingResult<WearableDTO> dto)
        {
            ResolveDTO(dto);
            IsLoading = false;
        }

        public bool IsOnChain()
        {
            var id = this.GetUrn().ToString();
            bool startsWith = id.StartsWith("urn:decentraland:off-chain:base-avatars:", StringComparison.Ordinal);
            return startsWith == false;
        }

        public AvatarAttachmentDTO DTO => Model.Asset!;

        public string GetCategory() =>
            Model.Asset!.metadata.data.category;

        public bool TryResolveDTO(StreamableLoadingResult<WearableDTO> result)
        {
            if (Model.IsInitialized)
                return false;

            ResolveDTO(result);
            return true;
        }

        private void ResolveDTO(StreamableLoadingResult<WearableDTO> result)
        {
            Model = result;

            if (IsFacialFeature())
                Type = WearableType.FacialFeature;
            else if (IsBodyShape())
                Type = WearableType.BodyShape;
            else
                Type = WearableType.Regular;
        }

        public bool TryGetFileHashConditional(BodyShape bodyShape, Func<string, bool> contentMatch, out string? hash)
        {
            AvatarAttachmentDTO wearableDTO = DTO;

            for (var i = 0; i < wearableDTO.Metadata.AbstractData.representations.Length; i++)
            {
                AvatarAttachmentDTO.Representation representation = wearableDTO.Metadata.AbstractData.representations[i];

                if (representation.bodyShapes.Contains(bodyShape))
                {
                    string mainFileKey = representation.mainFile;

                    for (var j = 0; j < representation.contents.Length; j++)
                    {
                        string? contentKey = representation.contents[j];

                        if (mainFileKey != contentKey && contentMatch(contentKey))
                        {
                            IAvatarAttachment attachment = this;
                            return attachment.TryGetContentHashByKey(contentKey, out hash);
                        }
                    }

                    break;
                }
            }

            hash = null;
            return false;
        }

        public void GetHidingList(string bodyShapeType, HashSet<string> hideListResult)
        {
            AvatarAttachmentDTO dto = DTO;

            AvatarAttachmentDTO.Representation? representation = GetRepresentation(bodyShapeType);
            AvatarAttachmentDTO.DataBase? data = dto.Metadata.AbstractData;

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

        public bool IsCompatibleWithBodyShape(string bodyShape)
        {
            foreach (AvatarAttachmentDTO.Representation dataRepresentation in DTO.Metadata.AbstractData.representations)
            {
                if (dataRepresentation.bodyShapes.Contains(bodyShape))
                    return true;
            }

            return false;
        }

        public bool HasSameModelsForAllGenders()
        {
            IAvatarAttachment attachment = this;

            attachment.TryGetMainFileHash(BodyShape.MALE, out string? maleHash);
            attachment.TryGetMainFileHash(BodyShape.FEMALE, out string? femaleHash);

            return maleHash == femaleHash;
        }

        private bool IsBodyShape() =>
            GetCategory().Equals(WearablesConstants.Categories.BODY_SHAPE);

        private bool IsSkin() =>
            GetCategory() == WearablesConstants.Categories.SKIN;

        private bool IsFacialFeature() =>
            WearablesConstants.FACIAL_FEATURES.Contains(GetCategory());

        private AvatarAttachmentDTO.Representation? GetRepresentation(string bodyShapeType)
        {
            AvatarAttachmentDTO dto = DTO;

            foreach (AvatarAttachmentDTO.Representation representation in dto.Metadata.AbstractData.representations)
            {
                if (representation.bodyShapes.Contains(bodyShapeType))
                    return representation;
            }

            return null;
        }

        private string[]? GetReplacesList(string bodyShapeType)
        {
            AvatarAttachmentDTO.Representation? representation = GetRepresentation(bodyShapeType);
            AvatarAttachmentDTO dto = DTO;

            if (representation == null)
            {
                ReportHub.LogWarning(ReportCategory.WEARABLE, $"Wearable {dto.id} has no representation for body shape {bodyShapeType}");
                return dto.Metadata.AbstractData.replaces;
            }

            Assert.IsTrue(representation.HasValue);

            if (representation?.overrideReplaces == null || representation.Value.overrideReplaces.Length == 0)
                return dto.Metadata.AbstractData.replaces;

            return representation.Value.overrideReplaces;
        }

        public static HashSet<string> ComposeHiddenCategories(string bodyShapeId, List<IWearable> wearables)
        {
            HashSet<string> result = new HashSet<string>();

            foreach (var wearableItem in wearables)
            {
                if (wearableItem == null)
                    continue;

                if (result.Contains(wearableItem.GetCategory()))
                    continue;

                HashSet<string> wearableHidesList = new (StringComparer.OrdinalIgnoreCase);
                wearableItem.GetHidingList(bodyShapeId, wearableHidesList);

                result.UnionWith(wearableHidesList);
            }

            return result;
        }
    }
}
