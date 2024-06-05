using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
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
        public StreamableLoadingResult<SceneAssetBundleManifest>? ManifestResult { get; set; }

        public WearableAssets[] WearableAssetResults { get; } = new WearableAssets[BodyShape.COUNT];

        public StreamableLoadingResult<WearableDTO> WearableDTO { get; private set; }

        public StreamableLoadingResult<Sprite>? ThumbnailAssetResult { get; set; }

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

        public AvatarAttachmentDTO GetDTO() =>
            WearableDTO.Asset!;

        public string GetCategory() =>
            WearableDTO.Asset!.metadata.data.category;

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

        public bool TryGetFileHashConditional(BodyShape bodyShape, Func<string, bool> contentMatch, out string? hash)
        {
            AvatarAttachmentDTO wearableDTO = GetDTO();

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
            AvatarAttachmentDTO dto = GetDTO();

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
            foreach (AvatarAttachmentDTO.Representation dataRepresentation in GetDTO().Metadata.AbstractData.representations)
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
            AvatarAttachmentDTO dto = GetDTO();

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
            AvatarAttachmentDTO dto = GetDTO();

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
    }
}
