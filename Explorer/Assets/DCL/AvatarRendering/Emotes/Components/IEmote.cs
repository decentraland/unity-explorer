using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Common.Components;

namespace DCL.AvatarRendering.Emotes
{
    public interface IEmote : IAvatarAttachment<EmoteDTO>
    {
        StreamableLoadingResult<AudioClipData>?[] AudioAssetResults { get; }
        StreamableLoadingResult<AttachmentRegularAsset>?[] AssetResults { get; }

        bool IsLooping();

        bool HasSameClipForAllGenders();
    }
}
