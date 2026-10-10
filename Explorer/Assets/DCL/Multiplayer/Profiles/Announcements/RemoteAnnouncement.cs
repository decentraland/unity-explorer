using DCL.Multiplayer.Connections.Rooms;

namespace DCL.Multiplayer.Profiles.Announcements
{
    public readonly struct RemoteAnnouncement
    {
        public readonly int Version;
        public readonly string WalletId;
        public readonly RoomSource FromRoom;

        public RemoteAnnouncement(int version, string walletId, RoomSource fromRoom)
        {
            Version = version;
            WalletId = walletId;
            FromRoom = fromRoom;
        }

        public override string ToString() =>
            $"(RemoteAnnouncement: (Version: {Version}, WalletId: {WalletId}))";
    }
}
