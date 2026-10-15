using CommunicationData.URLHelpers;
using DCL.ECSComponents;
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
        public readonly double Timestamp;
        public readonly AvatarEmoteMask Mask;
        public readonly IEmotesMessageBus? Source;
        public readonly ulong RetryGeneration;

        public RemoteEmoteIntention(URN emoteId, string walletId, double timestamp, AvatarEmoteMask mask,
            IEmotesMessageBus? source = null, ulong retryGeneration = 0)
        {
            EmoteId = emoteId;
            WalletId = walletId;
            Timestamp = timestamp;
            Mask = mask;
            Source = source;
            RetryGeneration = retryGeneration;
        }

        public bool Equals(RemoteEmoteIntention other) =>
            EmoteId.Equals(other.EmoteId) && WalletId == other.WalletId && Math.Abs(Timestamp - other.Timestamp) < 0.001 && Mask == other.Mask;

        public override bool Equals(object? obj) =>
            obj is RemoteEmoteIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(EmoteId, WalletId, Timestamp);
    }
}
