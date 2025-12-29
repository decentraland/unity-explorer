using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using System.Linq;

namespace DCL.AvatarRendering.Wearables.Components
{
    public class TrimmedWearable : ITrimmedWearable
    {
        public TrimmedWearable(TrimmedWearableDTO model)
        {
            TrimmedModel = new StreamableLoadingResult<TrimmedWearableDTO>(model);
        }

        public StreamableLoadingResult<SpriteData>.WithFallback? ThumbnailAssetResult { get; set; }
        public TrimmedAvatarAttachmentDTO TrimmedDTO => TrimmedModel.Asset!;
        public StreamableLoadingResult<TrimmedWearableDTO> TrimmedModel { get; set; }

        public int Amount { get; set; }

        public bool IsCompatibleWithBodyShape(string bodyShape)
        {
            foreach (var dataRepresentation in TrimmedDTO.Metadata.AbstractData.representations)
                if (dataRepresentation.bodyShapes.Contains(bodyShape))
                    return true;

            return false;
        }
    }
}