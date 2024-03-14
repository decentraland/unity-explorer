using DCL.AvatarRendering.Wearables.Components;
using ECS.StreamableLoading.Common.Components;

namespace DCL.AvatarRendering.Emotes
{
    public interface IEmote : IAvatarAttachment
    {
        StreamableLoadingResult<EmoteDTO> Model { get; set; }
    }
}
