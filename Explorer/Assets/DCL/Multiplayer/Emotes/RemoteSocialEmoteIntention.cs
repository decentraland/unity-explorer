using CommunicationData.URLHelpers;
using System;

namespace DCL.Multiplayer.Emotes
{
    /// <summary>
    ///     A social emote event relayed by the Pulse server, ordered by server tick: either the start animation of an
    ///     initiator (<see cref="OutcomeIndex" /> is <see cref="NO_OUTCOME" />) or the outcome animation of one participant.
    ///     Every event of one interaction carries the same server-assigned <see cref="InteractionId" />.
    /// </summary>
    public readonly struct RemoteSocialEmoteIntention : IEquatable<RemoteSocialEmoteIntention>
    {
        public const int NO_OUTCOME = -1;

        public readonly uint InteractionId;
        public readonly URN EmoteId;

        /// <summary>
        ///     The participant this event is about.
        /// </summary>
        public readonly string WalletId;

        public readonly string InitiatorWalletId;

        /// <summary>
        ///     Empty when the start animation is not directed at a specific avatar.
        /// </summary>
        public readonly string TargetWalletId;

        public readonly int OutcomeIndex;
        public readonly double Timestamp;

        public bool IsStart => OutcomeIndex == NO_OUTCOME;

        public bool IsFromInitiator => WalletId == InitiatorWalletId;

        public RemoteSocialEmoteIntention(uint interactionId, URN emoteId, string walletId, string initiatorWalletId, string targetWalletId, int outcomeIndex, double timestamp)
        {
            InteractionId = interactionId;
            EmoteId = emoteId;
            WalletId = walletId;
            InitiatorWalletId = initiatorWalletId;
            TargetWalletId = targetWalletId;
            OutcomeIndex = outcomeIndex;
            Timestamp = timestamp;
        }

        public bool Equals(RemoteSocialEmoteIntention other) =>
            InteractionId == other.InteractionId && WalletId == other.WalletId && OutcomeIndex == other.OutcomeIndex && Math.Abs(Timestamp - other.Timestamp) < 0.001;

        public override bool Equals(object? obj) =>
            obj is RemoteSocialEmoteIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(InteractionId, WalletId, OutcomeIndex);
    }
}
