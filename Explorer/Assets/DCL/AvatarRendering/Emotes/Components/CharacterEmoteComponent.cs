using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using Utility.Animations;

namespace DCL.AvatarRendering.Emotes
{
    public struct CharacterEmoteComponent
    {
        public URN EmoteUrn;
        public bool EmoteLoop;
        public EmoteReferences? CurrentEmoteReference;
        public int CurrentAnimationTag;
        // TODO: Replace StopEmote with component StopEmoteIntent
        public bool StopEmote;
        public EmoteDTO.EmoteMetadataDto? Metadata;
        // TODO: Put all the fields of the social emote into an inner class SocialEmote
        // TODO: Is it redundant to store this here when the interaction state can be checked from anywhere using the Profile.UserId?
        public bool IsPlayingSocialEmoteOutcome;
        public int CurrentSocialEmoteOutcome;
        public bool IsReactingToSocialEmote;
        public string SocialEmoteInitiatorWalletAddress;
        public bool HasOutcomeAnimationStarted;
        public string TargetAvatarWalletAddress;
        public int SocialEmoteInteractionId;

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
                if (CurrentEmoteReference != null && CurrentEmoteReference.legacy)
                    return CurrentEmoteReference.animationComp!.isPlaying;

                // For mecanim animations, we check the actual animator tag
                // We do that because we can be in a different state even if triggers have been set (e.g., waiting for a jump to finish)
                return CurrentAnimationTag == AnimationHashes.EMOTE || CurrentAnimationTag == AnimationHashes.EMOTE_LOOP;
            }
        }

        public void Reset()
        {
            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "-emote Reset- " + EmoteUrn);
            EmoteUrn = default;
            EmoteLoop = false;
            StopEmote = false;
            Metadata = null;
            IsPlayingSocialEmoteOutcome = false;
            CurrentSocialEmoteOutcome = -1;
            IsReactingToSocialEmote = false;
            SocialEmoteInitiatorWalletAddress = string.Empty;
            HasOutcomeAnimationStarted = false;
            TargetAvatarWalletAddress = string.Empty;
            SocialEmoteInteractionId = 0;
            // These fields are not reset, on purpose (old code depends on it)
            //        CurrentEmoteReference = null;
            //        CurrentAnimationTag = 0;
        }
    }
}
