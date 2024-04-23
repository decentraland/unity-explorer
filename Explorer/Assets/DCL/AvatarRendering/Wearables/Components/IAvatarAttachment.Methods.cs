using CommunicationData.URLHelpers;
using System.Linq;

namespace DCL.AvatarRendering.Wearables.Components
{
    public partial interface IAvatarAttachment
    {
        private const string THUMBNAIL_DEFAULT_KEY = "thumbnail.png";

        // Methods are part of the interface instead of extensions to enable mocking for tests

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

        URLPath GetThumbnail()
        {
            AvatarAttachmentDTO wearableDTO = GetDTO();

            string thumbnailHash = wearableDTO.Metadata.thumbnail;

            if (thumbnailHash == THUMBNAIL_DEFAULT_KEY && TryGetContentHashByKey(THUMBNAIL_DEFAULT_KEY, out string hash))
                thumbnailHash = hash;

            return new URLPath(thumbnailHash);
        }
    }
}
