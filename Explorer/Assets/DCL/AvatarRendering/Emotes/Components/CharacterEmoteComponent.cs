using CommunicationData.URLHelpers;
using DCL.ECSComponents;
using System.Runtime.CompilerServices;
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

        public readonly bool IsPlayingEmote =>
            currentAnimationTag == AnimationHashes.EMOTE || currentAnimationTag == AnimationHashes.EMOTE_LOOP;

        public readonly int CurrentAnimationTag => currentAnimationTag;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
