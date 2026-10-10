using CommunicationData.URLHelpers;
using DCL.ECSComponents;
using System.Runtime.CompilerServices;
using Utility.Animations;

namespace DCL.AvatarRendering.Emotes
{
    /// <summary>
    ///     One-shot record of an emote playback start, pending propagation to the scene world.
    /// </summary>
    public struct EmoteStartEvent
    {
        public URN Urn;
        public bool Loop;
        public bool IsSet;
    }

    /// <summary>
    ///     One-shot record of an emote playback stop, pending propagation to the scene world.
    ///     Reason distinguishes a natural finish (EsFinished) from any interruption (EsInterrupted).
    /// </summary>
    public struct EmoteStopEvent
    {
        public URN Urn;
        public bool Loop;
        public EmoteState Reason;
        public bool IsSet;
    }

    public struct CharacterEmoteComponent
    {
        public URN EmoteUrn;
        public bool EmoteLoop;
        public EmoteReferences? CurrentEmoteReference;
        public bool StopEmote;
        public AvatarEmoteMask Mask;

        /// <summary>
        ///     Pending one-shot playback events consumed by AvatarEmoteCommandPropagationSystem.
        ///     Deliberately not cleared by <see cref="Reset" />: a stop is recorded in the same call that resets
        ///     the component, and the event must survive until the propagation system consumes it.
        /// </summary>
        public EmoteStartEvent PendingStart;
        public EmoteStopEvent PendingStop;

        private int currentAnimationTag;

        public float PlayingEmoteDuration => CurrentEmoteReference?.avatarClip
            ? CurrentEmoteReference.avatarClip.length * (CurrentEmoteReference.animatorComp != null ? CurrentEmoteReference.animatorComp.speed : 1f)
            : 0f;

        public readonly bool IsPlayingEmote =>
            (CurrentEmoteReference != null && CurrentEmoteReference.legacy)
            || currentAnimationTag == AnimationHashes.EMOTE
            || currentAnimationTag == AnimationHashes.EMOTE_LOOP;

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
