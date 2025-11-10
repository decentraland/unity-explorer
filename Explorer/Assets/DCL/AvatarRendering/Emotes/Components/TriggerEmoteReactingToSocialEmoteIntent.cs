using Arch.Core;

namespace DCL.AvatarRendering.Emotes
{
    /// <summary>
    /// Component to add to an entity of an avatar it has to play an outcome animation as a reaction to a start animation of other avatar.
    /// </summary>
    public readonly struct TriggerEmoteReactingToSocialEmoteIntent
    {
        /// <summary>
        /// The URN of the social emote.
        /// </summary>
        public readonly string? TriggeredEmoteUrn;

        /// <summary>
        /// The outcome animation to play (starting at zero).
        /// </summary>
        public readonly int OutcomeIndex;

        /// <summary>
        /// The wallet address of the player who initiated the interaction (start animation).
        /// </summary>
        public readonly string InitiatorWalletAddress;

        /// <summary>
        ///
        /// </summary>
        public readonly Entity InitiatorEntity;

        /// <summary>
        /// The ID that identifies the interaction for which the avatar is to play the animation.
        /// </summary>
        public readonly int InteractionId;

        public TriggerEmoteReactingToSocialEmoteIntent(string? triggeredEmoteUrn, int outcomeIndex, string initiatorWalletAddress, Entity initiatorEntity, int interactionId)
        {
            this.TriggeredEmoteUrn = triggeredEmoteUrn;
            this.OutcomeIndex = outcomeIndex;
            this.InitiatorWalletAddress = initiatorWalletAddress;
            this.InitiatorEntity = initiatorEntity;
            this.InteractionId = interactionId;
        }
    }
}
