using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;

namespace DCL.AvatarRendering.AvatarShape.Tests
{
    public class FakeTrimmedWearable : ITrimmedWearable
    {
        public StreamableLoadingResult<SpriteData>.WithFallback? ThumbnailAssetResult { get; set; }
        public TrimmedAvatarAttachmentDTO TrimmedDTO { get; }
        public StreamableLoadingResult<TrimmedWearableDTO> TrimmedModel { get; set; }

        public FakeTrimmedWearable(
            TrimmedWearableDTO dto,
            StreamableLoadingResult<TrimmedWearableDTO> model = default
        )
        {
            TrimmedDTO = dto;
            TrimmedModel = model;
        }

        public int Amount { get; set; }

        public bool IsCompatibleWithBodyShape(string bodyShape)
        {
            return false;
        }
    }
}