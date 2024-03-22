using DCL.AvatarRendering.Wearables.Components;
using ECS.StreamableLoading.Common.Components;
using UnityEngine;

namespace DCL.AvatarRendering.Emotes.Components
{
    public interface IEmote : IAvatarAttachment
    {
        StreamableLoadingResult<EmoteDTO> Model { get; set; }
        StreamableLoadingResult<AudioClip>? AudioAssetResult { get; set; }

        bool IsLooping();
    }
}
