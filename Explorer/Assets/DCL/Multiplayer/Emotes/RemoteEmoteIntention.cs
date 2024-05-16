using CommunicationData.URLHelpers;
using System;

namespace DCL.Multiplayer.Emotes
{
    /// <summary>
    /// Intention to launch an emote from a remote player.
    /// </summary>
    public readonly struct RemoteEmoteIntention : IEquatable<RemoteEmoteIntention>
    {
        public readonly URN EmoteId;
        public readonly string WalletId;

        public RemoteEmoteIntention(URN emoteId, string walletId)
        {
            EmoteId = emoteId;
            WalletId = walletId;
        }

        public bool Equals(RemoteEmoteIntention other) =>
            EmoteId.Equals(other.EmoteId) && WalletId == other.WalletId;

        public override bool Equals(object? obj) =>
            obj is RemoteEmoteIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(EmoteId, WalletId);
    }
}
