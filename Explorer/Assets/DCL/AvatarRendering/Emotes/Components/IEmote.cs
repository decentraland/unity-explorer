using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common.Components;
using UnityEngine;

namespace DCL.AvatarRendering.Emotes
{
    public interface IEmote : IAvatarAttachment<EmoteDTO>
    {
        StreamableLoadingResult<AudioClip>?[] AudioAssetResults { get; set; }
        StreamableLoadingResult<WearableRegularAsset>?[] AssetResults { get; }

        bool IsLooping();
    }
}
