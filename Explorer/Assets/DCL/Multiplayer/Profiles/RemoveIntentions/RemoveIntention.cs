using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Profiles.Entities;

namespace DCL.Multiplayer.Profiles.RemoveIntentions
{
    public readonly struct RemoveIntention
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
    }
}
