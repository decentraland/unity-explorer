using System;

namespace DCL.Multiplayer.Emotes
{
    public struct RemoteEmoteStopIntention
    {
        public readonly string WalletId;
        public readonly double Timestamp;

        public RemoteEmoteStopIntention(string walletId, double timestamp)
        {
            WalletId = walletId;
            Timestamp = timestamp;
        }

        public bool Equals(RemoteEmoteStopIntention other) =>
            WalletId == other.WalletId && Math.Abs(Timestamp - other.Timestamp) < 0.001;

        public override bool Equals(object? obj) =>
            obj is RemoteEmoteStopIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(WalletId, Timestamp);
    }
}
