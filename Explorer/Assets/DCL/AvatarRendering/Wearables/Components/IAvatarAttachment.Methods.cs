using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Assertions;

namespace DCL.AvatarRendering.Wearables.Components
{
    public partial interface IAvatarAttachment
    {
        private const string THUMBNAIL_DEFAULT_KEY = "thumbnail.png";

        // Methods are part of the interface instead of extensions to enable mocking for tests

        protected internal bool IsBodyShape() =>
            GetCategory().Equals(WearablesConstants.Categories.BODY_SHAPE);

        protected internal bool IsSkin() =>
            GetCategory() == WearablesConstants.Categories.SKIN;

        protected internal bool IsFacialFeature() =>
            WearablesConstants.FACIAL_FEATURES.Contains(GetCategory());

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

        bool TryGetContentHashByKey(string key, out string hash)
        {
            AvatarAttachmentDTO wearableDTO = GetDTO();

            for (var i = 0; i < wearableDTO.content.Length; i++)
            {
                if (wearableDTO.content[i].file == key)
                {
                    hash = wearableDTO.content[i].hash;
                    return true;
                }
            }

            hash = null;
            return false;
        }

        bool TryGetFileHashConditional(BodyShape bodyShape, Func<string, bool> contentMatch, out string? hash)
        {
            AvatarAttachmentDTO wearableDTO = GetDTO();

            for (var i = 0; i < wearableDTO.Metadata.AbstractData.representations.Length; i++)
            {
                var representation = wearableDTO.Metadata.AbstractData.representations[i];

                if (representation.bodyShapes.Contains(bodyShape))
                {
                    string mainFileKey = representation.mainFile;

                    for (var j = 0; j < representation.contents.Length; j++)
                    {
                        var contentKey = representation.contents[j];

                        if (mainFileKey != contentKey && contentMatch(contentKey))
                            return TryGetContentHashByKey( contentKey, out hash);
                    }

                    break;
                }
            }

            hash = null;
            return false;
        }

        URLPath GetThumbnail()
        {
            AvatarAttachmentDTO wearableDTO = GetDTO();

            string thumbnailHash = wearableDTO.Metadata.thumbnail;

            if (thumbnailHash == THUMBNAIL_DEFAULT_KEY && TryGetContentHashByKey(THUMBNAIL_DEFAULT_KEY, out string hash))
                thumbnailHash = hash;

            return new URLPath(thumbnailHash);
        }

        void GetHidingList(string bodyShapeType, HashSet<string> hideListResult)
        {
            var dto = GetDTO();

            var representation = GetRepresentation(bodyShapeType);
            var data = dto.Metadata.AbstractData;

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

        bool IsCompatibleWithBodyShape(string bodyShape)
        {
            foreach (var dataRepresentation in GetDTO().Metadata.AbstractData.representations)
            {
                if (dataRepresentation.bodyShapes.Contains(bodyShape))
                    return true;
            }

            return false;
        }

        private AvatarAttachmentDTO.Representation? GetRepresentation(string bodyShapeType)
        {
            var dto = GetDTO();

            foreach (AvatarAttachmentDTO.Representation representation in dto.Metadata.AbstractData.representations)
            {
                if (representation.bodyShapes.Contains(bodyShapeType))
                    return representation;
            }

            return null;
        }

        private string[]? GetReplacesList(string bodyShapeType)
        {
            var representation = GetRepresentation(bodyShapeType);
            var dto = GetDTO();

            Assert.IsTrue(representation.HasValue);

            if (representation?.overrideReplaces == null || representation.Value.overrideReplaces.Length == 0)
                return dto.Metadata.AbstractData.replaces;

            return representation.Value.overrideReplaces;
        }
    }
}
