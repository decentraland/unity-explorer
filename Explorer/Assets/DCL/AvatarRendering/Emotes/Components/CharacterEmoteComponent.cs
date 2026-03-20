using CommunicationData.URLHelpers;
using DCL.ECSComponents;
using Utility.Animations;

namespace DCL.AvatarRendering.Emotes
{
    public struct CharacterEmoteComponent
    {
        public URN EmoteUrn;
        public bool EmoteLoop;
        public EmoteReferences? CurrentEmoteReference;
        public bool StopEmote;
        public AvatarEmoteMask Mask;

        private int currentAnimationTag;

        public float PlayingEmoteDuration => CurrentEmoteReference?.avatarClip
            ? CurrentEmoteReference.avatarClip.length * CurrentEmoteReference.animatorComp!.speed
            : 0f;

        /// <summary>
        ///     Whether a full-body emote is being played on the base layer.
        /// </summary>
        public readonly bool IsPlayingEmote
        {
            get
            {
                // Legacy clips are handled with the legacy animation component
                if (CurrentEmoteReference && CurrentEmoteReference.legacy) return CurrentEmoteReference.animationComp!.isPlaying;

                return currentAnimationTag == AnimationHashes.EMOTE || currentAnimationTag == AnimationHashes.EMOTE_LOOP;
            }
        }

        public readonly bool IsPlayingLegacyEmote => CurrentEmoteReference && CurrentEmoteReference.legacy;

        public readonly int CurrentAnimationTag => currentAnimationTag;

        public void SetAnimationTag(int tag) => currentAnimationTag = tag;

        public void Reset()
        {
            EmoteLoop = false;
            CurrentEmoteReference = null;
            StopEmote = false;
            Mask = AvatarEmoteMask.AemFullBody;
        }
    }
}
