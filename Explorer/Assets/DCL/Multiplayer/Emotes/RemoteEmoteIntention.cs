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
            public bool IsUsingOutcomeAnimation;
            public int OutcomeIndex;
            public bool IsReacting;
            public string InitiatorWalletAddress;
            public string TargetAvatarWalletAddress;
            public int InteractionId;
        }

        public readonly URN EmoteId;
        public readonly string WalletId;
        public readonly float Timestamp;
        public readonly bool IsStopping;
        public readonly bool IsRepeating;
        public readonly SocialEmoteData SocialEmote;

        public RemoteEmoteIntention(URN emoteId, string walletId, float timestamp, bool isUsingSocialEmoteOutcome, int socialEmoteOutcomeIndex, bool isReactingToSocialEmote, string socialEmoteInitiatorWalletAddress, string targetAvatarWalletAddress, bool isStopping, bool isRepeating, int socialEmoteInteractionId)
        {
            EmoteId = emoteId;
            WalletId = walletId;
            Timestamp = timestamp;
            SocialEmote = new SocialEmoteData()
            {
                IsUsingOutcomeAnimation = isUsingSocialEmoteOutcome,
                OutcomeIndex = socialEmoteOutcomeIndex,
                IsReacting = isReactingToSocialEmote,
                InitiatorWalletAddress = socialEmoteInitiatorWalletAddress,
                TargetAvatarWalletAddress = targetAvatarWalletAddress,
                InteractionId = socialEmoteInteractionId
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
