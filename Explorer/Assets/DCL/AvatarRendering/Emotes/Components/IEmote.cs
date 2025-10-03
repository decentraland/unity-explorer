using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Emotes
{
    public interface IEmote : IAvatarAttachment<EmoteDTO>
    {
        struct SocialEmoteOutcomeAudioAssetResult
        {
            public string EmoteId;
            public int Outcome;
            public StreamableLoadingResult<AudioClipData> Result;
        }

        StreamableLoadingResult<AudioClipData>?[] AudioAssetResults { get; }
        StreamableLoadingResult<AttachmentRegularAsset>?[] AssetResults { get; }
        List<StreamableLoadingResult<AudioClipData>> SocialEmoteOutcomeAudioAssetResults { get; }

        bool IsSocial { get; }

        bool IsLooping();

        bool HasSameClipForAllGenders();

        public static IEmote NewEmpty() =>
            new Emote();
    }
}
