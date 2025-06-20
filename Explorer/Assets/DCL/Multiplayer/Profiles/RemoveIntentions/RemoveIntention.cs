using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Profiles.Entities;
using System;

namespace DCL.Multiplayer.Profiles.RemoveIntentions
{
    public readonly struct RemoveIntention : IEquatable<RemoveIntention>
    {
        public readonly string WalletId;
        public readonly RoomSource FromRoom;

        public RemoveIntention(string walletId, RoomSource fromRoom)
        {
            WalletId = walletId;
            FromRoom = fromRoom;
        }

        public override string ToString() =>
            $"(RemoveIntention: (WalletId: {WalletId}, From Room: {FromRoom}))";

        public bool Equals(RemoveIntention other) =>
            WalletId == other.WalletId && FromRoom == other.FromRoom;

        public override bool Equals(object? obj) =>
            obj is RemoveIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(WalletId, (int) FromRoom);
    }
}
