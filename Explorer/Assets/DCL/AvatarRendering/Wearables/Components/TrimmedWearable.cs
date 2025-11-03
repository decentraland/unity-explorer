using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Ipfs;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using System;
using System.Linq;

namespace DCL.AvatarRendering.Wearables.Components
{
    [Serializable]
    public class TrimmedWearable : ITrimmedWearable
    {
        public TrimmedWearable() { }

        public bool IsLoading { get; private set; }

        public void UpdateLoadingStatus(bool isLoading) =>
            IsLoading = isLoading;

        public StreamableLoadingResult<SpriteData>.WithFallback? ThumbnailAssetResult { get; set; }
        public TrimmedAvatarAttachmentDTO DTO => Model.Asset!;
        public StreamableLoadingResult<TrimmedWearableDTO> Model { get; set; }

        public bool IsOnChain() =>
            !this.GetUrn().ToString().StartsWith("urn:decentraland:off-chain:base-avatars:", StringComparison.Ordinal);

        public WearableAssets[] WearableAssetResults { get; } = new WearableAssets[BodyShape.COUNT];


        public bool IsCompatibleWithBodyShape(string bodyShape)
        {
            foreach (AvatarAttachmentDTO.Representation dataRepresentation in DTO.Metadata.AbstractData.representations)
                if (dataRepresentation.bodyShapes.Contains(bodyShape))
                    return true;

            return false;
        }
    }
}
