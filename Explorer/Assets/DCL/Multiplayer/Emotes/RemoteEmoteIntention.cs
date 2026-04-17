using CommunicationData.URLHelpers;
using DCL.ECSComponents;
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
        public readonly AvatarEmoteMask Mask;

        public RemoteEmoteIntention(URN emoteId, string walletId, float timestamp, AvatarEmoteMask mask)
        {
            EmoteId = emoteId;
            WalletId = walletId;
            Timestamp = timestamp;
            Mask = mask;
        }

        public bool Equals(RemoteEmoteIntention other) =>
            EmoteId.Equals(other.EmoteId) && WalletId == other.WalletId && Mathf.Approximately(Timestamp, other.Timestamp) && Mask == other.Mask;

        public override bool Equals(object? obj) =>
            obj is RemoteEmoteIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(EmoteId, WalletId, Timestamp);
    }
}
