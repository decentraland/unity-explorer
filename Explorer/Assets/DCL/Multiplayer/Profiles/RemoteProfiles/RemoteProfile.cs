using DCL.Multiplayer.Connections.Rooms;
using DCL.Profiles;

namespace DCL.Multiplayer.Profiles.RemoteProfiles
{
    public readonly struct RemoteProfile
    {
        public readonly Profile Profile;
        public readonly string WalletId;
        public readonly RoomSource FromRoom;

        public RemoteProfile(Profile profile, string walletId, RoomSource fromRoom)
        {
            Profile = profile;
            WalletId = walletId;
            FromRoom = fromRoom;
        }
    }
}
