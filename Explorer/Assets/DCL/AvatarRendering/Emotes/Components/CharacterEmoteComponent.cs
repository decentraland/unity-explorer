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

        public bool IsPlayingEmote => CurrentEmoteReference;
        public float PlayingEmoteDuration => CurrentEmoteReference?.avatarClip
            ? CurrentEmoteReference.avatarClip.length * CurrentEmoteReference.animatorComp!.speed
            : 0f;

        public void Reset()
        {
            EmoteLoop = false;
            CurrentEmoteReference = null;
            StopEmote = false;
        }

        /// <summary>
        ///     Whether the avatar is currently playing an emote.
        /// </summary>
        public readonly bool IsPlayingEmote()
        {
            return CurrentAnimationTag == AnimationHashes.EMOTE || CurrentAnimationTag == AnimationHashes.EMOTE_LOOP;
        }
    }
}
