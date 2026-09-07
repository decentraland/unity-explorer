using CommunicationData.URLHelpers;

namespace DCL.AvatarRendering.Emotes
{
    public enum SocialEmoteRole : byte
    {
        Initiator,
        Receiver,
    }

    public enum SocialEmotePhase : byte
    {
        /// <summary>
        ///     The initiator loops the start animation and waits for a reaction.
        /// </summary>
        Started,

        /// <summary>
        ///     Both participants play their outcome animation.
        /// </summary>
        Outcome,

        /// <summary>
        ///     The interaction ended; the component is removed on the next update.
        /// </summary>
        Finished,
    }

    /// <summary>
    ///     Lives on every participant of a social emote interaction. Written only by <see cref="SocialEmoteInteractionSystem" />.
    /// </summary>
    public struct SocialEmoteInteractionComponent
    {
        public uint InteractionId;
        public URN EmoteId;
        public SocialEmoteRole Role;
        public SocialEmotePhase Phase;

        /// <summary>
        ///     Wallet of the other participant. Empty on an initiator nobody reacted to yet.
        /// </summary>
        public string PartnerWalletId;

        /// <summary>
        ///     Wallet the start animation was directed at. Empty when undirected or on a receiver.
        /// </summary>
        public string TargetWalletId;

        /// <summary>
        ///     <see cref="RemoteSocialEmoteIntention.NO_OUTCOME" /> until an outcome is played.
        /// </summary>
        public int OutcomeIndex;

        /// <summary>
        ///     Seconds spent in the current <see cref="Phase" />.
        /// </summary>
        public float ElapsedInPhase;
    }
}
