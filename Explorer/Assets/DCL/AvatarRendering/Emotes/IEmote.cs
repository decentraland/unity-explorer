using DCL.AvatarRendering.Wearables.Components;
using ECS.StreamableLoading.Common.Components;

namespace DCL.AvatarRendering.Emotes
{
    public interface IEmote : IAvatarNftAccessory
    {
        StreamableLoadingResult<EmoteDTO> Model { get; set; }
    }
}
