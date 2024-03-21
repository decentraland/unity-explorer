using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Linq;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public static class WearableDTOUtils
    {
        private const string THUMBNAIL_DEFAULT_KEY = "thumbnail.png";
        private static string contentKey;

        public static bool TryGetContentHashByKey(this IWearable wearable, string key, out string hash)
        {
            StreamableLoadingResult<WearableDTO> wearableDTO = wearable.WearableDTO;

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

        public static URLPath GetThumbnail(this IWearable wearable)
        {
            StreamableLoadingResult<WearableDTO> wearableDTO = wearable.WearableDTO;

            string thumbnailHash = wearableDTO.Asset.metadata.thumbnail;

            if (thumbnailHash == THUMBNAIL_DEFAULT_KEY && TryGetContentHashByKey(wearable, THUMBNAIL_DEFAULT_KEY, out var hash))
                thumbnailHash = hash;

            return new URLPath(thumbnailHash);
        }

        public static bool TryGetMainFileHash(this IWearable wearable, BodyShape bodyShape, out string hash)
        {
            StreamableLoadingResult<WearableDTO> wearableDTO = wearable.WearableDTO;

            // The length of arrays is small, so O(N) complexity is fine
            // Avoid iterator allocations with "for" loop
            for (var i = 0; i < wearableDTO.Asset.metadata.data.representations.Length; i++)
            {
                WearableDTO.WearableMetadataDto.Representation representation = wearableDTO.Asset.metadata.data.representations[i];

                if (representation.bodyShapes.Contains(bodyShape))
                    return TryGetContentHashByKey(wearable, representation.mainFile, out hash);
            }

            hash = null;
            return false;
        }

        public static bool TryGetFileHashConditional(this IWearable wearable, BodyShape bodyShape, Func<string, bool> contentMatch, out string hash)
        {
            StreamableLoadingResult<WearableDTO> wearableDTO = wearable.WearableDTO;

            for (var i = 0; i < wearableDTO.Asset.metadata.data.representations.Length; i++)
            {
                WearableDTO.WearableMetadataDto.Representation representation = wearableDTO.Asset.metadata.data.representations[i];

                if (representation.bodyShapes.Contains(bodyShape))
                {
                    string mainFileKey = representation.mainFile;

                    for (var j = 0; j < representation.contents.Length; j++)
                    {
                        contentKey = representation.contents[j];

                        if (mainFileKey != contentKey && contentMatch(contentKey))
                            return TryGetContentHashByKey(wearable, contentKey, out hash);
                    }

                    break;
                }
            }

            hash = null;
            return false;
        }
    }
}
