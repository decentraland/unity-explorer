using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using UnityEngine;
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
        public EmoteDTO.EmoteMetadataDto? Metadata;
        public bool IsPlayingSocialEmoteOutcome;
        public int CurrentSocialEmoteOutcome;
        public bool IsReactingToSocialEmote;
        public string SocialEmoteInitiatorWalletAddress;
        public bool HasOutcomeAnimationStarted;

        public float PlayingEmoteDuration => CurrentEmoteReference?.avatarClip
            ? CurrentEmoteReference.avatarClip.length * CurrentEmoteReference.animatorComp!.speed
            : 0f;

        /// <summary>
        ///     Whether an emote is being played.
        /// </summary>
        /// <remarks>
        ///     In Local Scene Development mode the method behaves slightly differently. Check the implementation for details.
        /// </remarks>
        public readonly bool IsPlayingEmote
        {
            get
            {
                // NOTE in Local Scene Development mode -- where legacy anims are allowed -- we will have different behavior

                // Legacy clips are handled with the legacy animation component
                if (CurrentEmoteReference && CurrentEmoteReference.legacy) return CurrentEmoteReference.animationComp!.isPlaying;

                // For mecanim animations, we check the actual animator tag
                // We do that because we can be in a different state even if triggers have been set (e.g., waiting for a jump to finish)
                return CurrentAnimationTag == AnimationHashes.EMOTE || CurrentAnimationTag == AnimationHashes.EMOTE_LOOP;
            }
        }

        public void Reset()
        {
            ReportHub.Log(ReportCategory.EMOTE_DEBUG, "-emote Reset-");
            EmoteLoop = false;
            CurrentEmoteReference = null;
            StopEmote = false;
            Metadata = null;
            IsPlayingSocialEmoteOutcome = false;
            CurrentSocialEmoteOutcome = -1;
            IsReactingToSocialEmote = false;
            SocialEmoteInitiatorWalletAddress = string.Empty;
            HasOutcomeAnimationStarted = false;
        }
    }
}
