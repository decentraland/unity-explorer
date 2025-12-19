using CommunicationData.URLHelpers;
using System;
using UnityEngine;

namespace DCL.Multiplayer.Emotes
{
    /// <summary>
    /// Intention to launch an emote from a remote player.
    /// </summary>
    public readonly struct RemoteEmoteIntention : IEquatable<RemoteEmoteIntention>
    {
        public struct SocialEmoteData
        {
            /// <summary>
            ///     Whether the avatar is playing an outcome animation of the emote.
            /// </summary>
            public bool IsUsingOutcomeAnimation;

            /// <summary>
            ///     The index of the outcome animation being played. -1 means no outcome is playing.
            /// </summary>
            public int OutcomeIndex;

            /// <summary>
            ///     Whether the avatar is reacting to the start animation of an initiator.
            /// </summary>
            public bool IsReacting;

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
        }

        public readonly URN EmoteId;
        public readonly string WalletId;
        public readonly float Timestamp;
        public readonly SocialEmoteData SocialEmote;

        /// <summary>
        ///     Whether the remote avatar's emote stopped and has to be stopped locally too.
        /// </summary>
        public readonly bool IsStopping;

        /// <summary>
        ///     Whether this is a repetition of a looping emote (true) or it is the first shot (false).
        /// </summary>
        public readonly bool IsRepeating;

        public RemoteEmoteIntention(URN emoteId, string walletId, float timestamp, bool isUsingSocialEmoteOutcome, int socialEmoteOutcomeIndex, bool isReactingToSocialEmote, string socialEmoteInitiatorWalletAddress, string targetAvatarWalletAddress, bool isStopping, bool isRepeating, int socialEmoteInteractionId)
        {
            EmoteId = emoteId;
            WalletId = walletId;
            Timestamp = timestamp;
            SocialEmote = new SocialEmoteData
            {
                IsUsingOutcomeAnimation = isUsingSocialEmoteOutcome, OutcomeIndex = socialEmoteOutcomeIndex, IsReacting = isReactingToSocialEmote, InitiatorWalletAddress = socialEmoteInitiatorWalletAddress,
                TargetAvatarWalletAddress = targetAvatarWalletAddress, InteractionId = socialEmoteInteractionId
            };
            IsStopping = isStopping;
            IsRepeating = isRepeating;
        }

        public bool Equals(RemoteEmoteIntention other) =>
            EmoteId.Equals(other.EmoteId) &&
            WalletId == other.WalletId &&
            Mathf.Approximately(Timestamp, other.Timestamp) &&
            SocialEmote.IsUsingOutcomeAnimation == other.SocialEmote.IsUsingOutcomeAnimation &&
            SocialEmote.OutcomeIndex == other.SocialEmote.OutcomeIndex &&
            SocialEmote.IsReacting == other.SocialEmote.IsReacting &&
            SocialEmote.InitiatorWalletAddress == other.SocialEmote.InitiatorWalletAddress &&
            SocialEmote.TargetAvatarWalletAddress == other.SocialEmote.TargetAvatarWalletAddress &&
            IsStopping == other.IsStopping &&
            IsRepeating == other.IsRepeating &&
            SocialEmote.InteractionId == other.SocialEmote.InteractionId;

        public override bool Equals(object? obj) =>
            obj is RemoteEmoteIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(EmoteId, WalletId, Timestamp, SocialEmote.IsUsingOutcomeAnimation, SocialEmote.OutcomeIndex, SocialEmote.IsReacting, SocialEmote.InitiatorWalletAddress, SocialEmote.TargetAvatarWalletAddress);
    }
}
