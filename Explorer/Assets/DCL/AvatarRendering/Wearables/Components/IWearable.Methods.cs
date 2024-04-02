using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Linq;

namespace DCL.AvatarRendering.Wearables.Components
{
    public partial interface IWearable
    {
        private const string THUMBNAIL_DEFAULT_KEY = "thumbnail.png";

        // Methods are part of the interface instead of extensions to enable mocking for tests

        bool TryGetMainFileHash(BodyShape bodyShape, out string hash)
        {
            StreamableLoadingResult<WearableDTO> wearableDTO = WearableDTO;

            // The length of arrays is small, so O(N) complexity is fine
            // Avoid iterator allocations with "for" loop
            for (var i = 0; i < wearableDTO.Asset.metadata.data.representations.Length; i++)
            {
                WearableDTO.WearableMetadataDto.Representation representation = wearableDTO.Asset.metadata.data.representations[i];

                if (representation.bodyShapes.Contains(bodyShape))
                    return TryGetContentHashByKey(representation.mainFile, out hash);
            }

            hash = null;
            return false;
        }

        bool TryGetContentHashByKey(string key, out string hash)
        {
            StreamableLoadingResult<WearableDTO> wearableDTO = WearableDTO;

            for (var i = 0; i < wearableDTO.Asset.content.Length; i++)
            {
                if (wearableDTO.Asset.content[i].file == key)
                {
                    hash = wearableDTO.Asset.content[i].hash;
                    return true;
                }
            }

            hash = null;
            return false;
        }

        bool TryGetFileHashConditional(BodyShape bodyShape, Func<string, bool> contentMatch, out string hash)
        {
            StreamableLoadingResult<WearableDTO> wearableDTO = WearableDTO;

            for (var i = 0; i < wearableDTO.Asset.metadata.data.representations.Length; i++)
            {
                WearableDTO.WearableMetadataDto.Representation representation = wearableDTO.Asset.metadata.data.representations[i];

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
            StreamableLoadingResult<WearableDTO> wearableDTO = WearableDTO;

            string thumbnailHash = wearableDTO.Asset.metadata.thumbnail;

            if (thumbnailHash == THUMBNAIL_DEFAULT_KEY && TryGetContentHashByKey(THUMBNAIL_DEFAULT_KEY, out var hash))
                thumbnailHash = hash;

            return new URLPath(thumbnailHash);
        }
    }
}
