using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common.Components;
using UnityEngine;
using IAvatarAttachment = DCL.AvatarRendering.Loading.Components.IAvatarAttachment;

namespace DCL.AvatarRendering.Emotes
{
    public interface IEmote : IAvatarAttachment
    {
        StreamableLoadingResult<EmoteDTO> Model { get; set; }
        StreamableLoadingResult<AudioClip>?[] AudioAssetResults { get; set; }
        StreamableLoadingResult<WearableRegularAsset>?[] AssetResults { get; }

        bool IsLooping();
    }
}
