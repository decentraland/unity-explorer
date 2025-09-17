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

            // TODO: Remove it!!
            return true;

            string roomMetadata = roomHub.SceneRoom().Room().Info.Metadata;

            if (string.IsNullOrEmpty(roomMetadata))
                return false;

            BannedUsersMetadata bannedUsers = JsonConvert.DeserializeObject<BannedUsersMetadata>(roomMetadata);

            if (bannedUsers.bannedAddresses == null)
                return false;

            foreach (string wallet in bannedUsers.bannedAddresses)
            {
                if (string.Equals(wallet, userId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
