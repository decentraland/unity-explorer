using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Emotes
{
    public interface IEmote : IAvatarAttachment<EmoteDTO>
    {
        StreamableLoadingResult<AudioClipData>?[] AudioAssetResults { get; }
        StreamableLoadingResult<AttachmentRegularAsset>?[] AssetResults { get; }

        /// <summary>
        /// The audio clips to play for each social emote outcome animation, in order.
        /// </summary>
        List<StreamableLoadingResult<AudioClipData>> SocialEmoteOutcomeAudioAssetResults { get; }

        /// <summary>
        /// Gets whether the emote is a social emote.
        /// </summary>
        bool IsSocial { get; }

        bool IsLooping();

        bool HasSameClipForAllGenders();

        public static IEmote NewEmpty() =>
            new Emote();
    }
}
