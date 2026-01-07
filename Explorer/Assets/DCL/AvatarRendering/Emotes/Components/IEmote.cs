using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Common.Components;

namespace DCL.AvatarRendering.Emotes
{
    public interface IEmote : IAvatarAttachment<EmoteDTO>
    {
        int Amount { get; set; }

        StreamableLoadingResult<AudioClipData>? AudioAssetResult { get; set; }
        StreamableLoadingResult<AttachmentRegularAsset>? AssetResult { get; set; }

        bool IsLooping();

        public static IEmote NewEmpty() =>
            new Emote();
    }
}
