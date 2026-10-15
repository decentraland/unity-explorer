using System;

namespace DCL.Multiplayer.Emotes
{
    public struct RemoteEmoteStopIntention : IEquatable<RemoteEmoteStopIntention>
    {
        public readonly string WalletId;
        public readonly double Timestamp;
        public readonly IEmotesMessageBus? Source;
        public readonly ulong RetryGeneration;

        public RemoteEmoteStopIntention(string walletId, double timestamp, IEmotesMessageBus? source = null, ulong retryGeneration = 0)
        {
            WalletId = walletId;
            Timestamp = timestamp;
            Source = source;
            RetryGeneration = retryGeneration;
        }

        public bool Equals(RemoteEmoteStopIntention other) =>
            WalletId == other.WalletId && Math.Abs(Timestamp - other.Timestamp) < 0.001;

        public override bool Equals(object? obj) =>
            obj is RemoteEmoteStopIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(WalletId, Timestamp);
    }
}
