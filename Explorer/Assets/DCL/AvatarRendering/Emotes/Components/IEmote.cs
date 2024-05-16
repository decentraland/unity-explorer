using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common.Components;
using UnityEngine;

namespace DCL.AvatarRendering.Emotes
{
    public interface IEmote : IAvatarAttachment
    {
        StreamableLoadingResult<EmoteDTO> Model { get; set; }
        StreamableLoadingResult<AudioClip>?[] AudioAssetResults { get; set; }
        StreamableLoadingResult<WearableRegularAsset>?[] WearableAssetResults { get; }

        bool IsLooping();
    }
}
