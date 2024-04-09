using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common.Components;

namespace DCL.AvatarRendering.Wearables.Components
{
    public interface IWearable : IAvatarAttachment
    {
        WearableType Type { get; }

        StreamableLoadingResult<WearableDTO> WearableDTO { get; }

        /// <summary>
        ///     Per <see cref="BodyShape" /> [MALE, FEMALE]
        /// </summary>
        WearableAssets[] WearableAssetResults { get; }

        /// <summary>
        ///     DTO must be resolved only one
        /// </summary>
        void ResolveDTO(StreamableLoadingResult<WearableDTO> result);
    }
}
