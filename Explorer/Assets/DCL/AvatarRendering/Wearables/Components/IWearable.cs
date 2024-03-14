using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common.Components;

namespace DCL.AvatarRendering.Wearables.Components
{
    public interface IWearable : IAvatarAttachment
    {
        StreamableLoadingResult<WearableDTO> WearableDTO { get; set; }
    }
}
