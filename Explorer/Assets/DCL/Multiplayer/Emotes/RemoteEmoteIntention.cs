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
        public readonly URN EmoteId;
        public readonly string WalletId;
        public readonly float Timestamp;
        public readonly bool IsUsingSocialOutcomeAnimation;
        public readonly int SocialEmoteOutcomeIndex;
        public readonly bool IsReactingToSocialEmote;
        public readonly string SocialEmoteInitiatorWalletAddress;
        public readonly string TargetAvatarWalletAddress;

        public RemoteEmoteIntention(URN emoteId, string walletId, float timestamp, bool isUsingSocialEmoteOutcome, int socialEmoteOutcomeIndex, bool isReactingToSocialEmote, string socialEmoteInitiatorWalletAddress, string targetAvatarWalletAddress)
        {
            EmoteId = emoteId;
            WalletId = walletId;
            Timestamp = timestamp;
            IsUsingSocialOutcomeAnimation = isUsingSocialEmoteOutcome;
            SocialEmoteOutcomeIndex = socialEmoteOutcomeIndex;
            IsReactingToSocialEmote = isReactingToSocialEmote;
            SocialEmoteInitiatorWalletAddress = socialEmoteInitiatorWalletAddress;
            TargetAvatarWalletAddress = targetAvatarWalletAddress;
        }

        public bool Equals(RemoteEmoteIntention other) =>
            EmoteId.Equals(other.EmoteId) &&
            WalletId == other.WalletId &&
            Mathf.Approximately(Timestamp, other.Timestamp) &&
            IsUsingSocialOutcomeAnimation == other.IsUsingSocialOutcomeAnimation &&
            SocialEmoteOutcomeIndex == other.SocialEmoteOutcomeIndex &&
            IsReactingToSocialEmote == other.IsReactingToSocialEmote &&
            SocialEmoteInitiatorWalletAddress == other.SocialEmoteInitiatorWalletAddress &&
            TargetAvatarWalletAddress == other.TargetAvatarWalletAddress;

        public override bool Equals(object? obj) =>
            obj is RemoteEmoteIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(EmoteId, WalletId, Timestamp, IsUsingSocialOutcomeAnimation, SocialEmoteOutcomeIndex, IsReactingToSocialEmote, SocialEmoteInitiatorWalletAddress, TargetAvatarWalletAddress);
    }
}
