using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common.Components;

namespace DCL.AvatarRendering.Wearables.Components
{
    public interface IWearable : IAvatarNftAccessory
    {
        StreamableLoadingResult<WearableDTO> WearableDTO { get; set; }
    }
}
