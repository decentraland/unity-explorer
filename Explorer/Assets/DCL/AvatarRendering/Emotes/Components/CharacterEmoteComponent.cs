using CommunicationData.URLHelpers;
using Utility.Animations;

namespace DCL.AvatarRendering.Emotes
{
    public struct CharacterEmoteComponent
    {
        public URN EmoteUrn;
        public bool EmoteLoop;
        public EmoteReferences? CurrentEmoteReference;
        public int CurrentAnimationTag;
        public bool StopEmote;
        public EmoteDTO.EmoteMetadataDto Metadata;
        public bool IsPlayingSocialEmoteOutcome;
        public int CurrentSocialEmoteOutcome;
        public bool IsReactingToSocialEmote;

        public bool IsPlayingEmote => CurrentAnimationTag == AnimationHashes.EMOTE || CurrentAnimationTag == AnimationHashes.EMOTE_LOOP;

        public float PlayingEmoteDuration => CurrentEmoteReference?.avatarClip
            ? CurrentEmoteReference.avatarClip.length * CurrentEmoteReference.animatorComp!.speed
            : 0f;

        public void Reset()
        {
            EmoteLoop = false;
            CurrentEmoteReference = null;
            StopEmote = false;
            IsPlayingSocialEmoteOutcome = false;
            CurrentSocialEmoteOutcome = -1;
            IsReactingToSocialEmote = false;
        }
    }
}
