using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using Utility.Animations;

namespace DCL.AvatarRendering.Emotes
{
    public struct CharacterEmoteComponent
    {
        public struct SocialEmoteData
        {
            public bool IsPlayingOutcome;
            public int CurrentOutcome;
            public bool IsReacting;
            public string InitiatorWalletAddress;
            public bool HasOutcomeAnimationStarted;
            public string TargetAvatarWalletAddress;
            public int InteractionId;
        }

        public URN EmoteUrn;
        public bool EmoteLoop;
        public EmoteReferences? CurrentEmoteReference;
        public int CurrentAnimationTag;
        // TODO: Replace StopEmote with component StopEmoteIntent
        public bool StopEmote;
        public EmoteDTO.EmoteMetadataDto? Metadata;
        // TODO: Is it redundant to store this here when the interaction state can be checked from anywhere using the Profile.UserId?
        public SocialEmoteData SocialEmote;

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
            SocialEmote.IsPlayingOutcome = false;
            SocialEmote.CurrentOutcome = -1;
            SocialEmote.IsReacting = false;
            SocialEmote.InitiatorWalletAddress = string.Empty;
            SocialEmote.HasOutcomeAnimationStarted = false;
            SocialEmote.TargetAvatarWalletAddress = string.Empty;
            SocialEmote.InteractionId = 0;
            // These fields are not reset, on purpose (old code depends on it)
            //        CurrentEmoteReference = null;
            //        CurrentAnimationTag = 0;
        }
    }
}
