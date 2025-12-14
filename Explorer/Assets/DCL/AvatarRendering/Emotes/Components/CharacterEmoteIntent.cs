using CommunicationData.URLHelpers;

namespace DCL.AvatarRendering.Emotes
{
    public enum TriggerSource
    {
        PREVIEW,
        SELF,
        REMOTE,
        SCENE,
    }

    public struct CharacterEmoteIntent
    {
        public struct SocialEmoteData
        {
            /// <summary>
            ///     Whether the avatar has to play an outcome animation of the emote.
            /// </summary>
            public bool UseOutcomeAnimation;

            /// <summary>
            ///     The index of the outcome animation to be played. -1 means no outcome will play.
            /// </summary>
            public int OutcomeIndex;

            /// <summary>
            ///     Whether the avatar has to play the reaction outcome animation of the emote (the animation of the receiver).
            /// </summary>
            public bool UseOutcomeReactionAnimation;

            /// <summary>
            ///     The wallet address of the initiator's player.
            /// </summary>
            public string InitiatorWalletAddress;

            /// <summary>
            ///     When a directed emote is sent, it is the wallet address of the player whose avatar will be able to react to the
            ///     emote.
            /// </summary>
            public string TargetAvatarWalletAddress;

            /// <summary>
            ///     The ID of the current interaction, set when an avatar starts a social emote.
            /// </summary>
            public int InteractionId;

            /// <summary>
            ///     Whether the outcome animation of the initiator has to be on hold until a message of a receiver playing the same
            ///     outcome animation for the same interaction arrives.
            ///     See explanation at RemoteEmotesSystem.
            /// </summary>
            public bool IsInitiatorOutcomeAnimationWaitingForReceiverAnimationLoop;
        }

        public URN EmoteId;
        public bool Spatial;
        public TriggerSource TriggerSource;
        public SocialEmoteData SocialEmote;

        /// <summary>
        ///     The wallet address of the player who is playing the emote.
        /// </summary>
        public string WalletAddress;

        /// <summary>
        ///     Whether this is a repetition of a looping emote (true) or it is the first shot (false).
        /// </summary>
        public bool IsRepeating;

        /// <summary>
        ///     The emote that could be loaded and is ready to be played.
        /// </summary>
        public IEmote? EmoteAsset;

        /// <summary>
        ///     Whether the emote was finally played.
        /// </summary>
        public bool HasPlayedEmote;

        public void UpdateRemoteId(URN emoteId)
        {
            WalletAddress = string.Empty;
            this.EmoteId = emoteId;
            this.Spatial = true;
            this.TriggerSource = TriggerSource.REMOTE;
            IsRepeating = false;
            SocialEmote.UseOutcomeAnimation = false;
            SocialEmote.OutcomeIndex = -1;
            SocialEmote.UseOutcomeReactionAnimation = false;
            SocialEmote.InitiatorWalletAddress = string.Empty;
            SocialEmote.TargetAvatarWalletAddress = string.Empty;
            SocialEmote.InteractionId = 0;
            SocialEmote.IsInitiatorOutcomeAnimationWaitingForReceiverAnimationLoop = false;
            EmoteAsset = null;
            HasPlayedEmote = false;
        }
    }
}
