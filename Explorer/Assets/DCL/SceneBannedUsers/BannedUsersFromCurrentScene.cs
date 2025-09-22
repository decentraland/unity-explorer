using CodeLess.Attributes;
using DCL.Multiplayer.Connections.RoomHubs;
using LiveKit.Proto;
using Newtonsoft.Json;
using System;
using System.Threading;

namespace DCL.SceneBannedUsers
{
    [Singleton]
    public partial class BannedUsersFromCurrentScene
    {
        private readonly IRoomHub roomHub;

        private CancellationTokenSource checkIfPlayerIsBannedCts;

        public BannedUsersFromCurrentScene(IRoomHub roomHub)
        {
            this.roomHub = roomHub;
        }

        public bool IsUserBanned(string userId)
        {
            if (roomHub.SceneRoom().Room().Info.ConnectionState != ConnectionState.ConnConnected)
                return false;

            string roomMetadata = roomHub.SceneRoom().Room().Info.Metadata;

            if (string.IsNullOrEmpty(roomMetadata))
                return false;

            BannedUsersRoomMetadata bannedUsersRoomMetadata = JsonConvert.DeserializeObject<BannedUsersRoomMetadata>(roomMetadata);

            if (bannedUsersRoomMetadata.bannedAddresses == null)
                return false;

            foreach (string wallet in bannedUsersRoomMetadata.bannedAddresses)
            {
                if (string.Equals(wallet, userId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
