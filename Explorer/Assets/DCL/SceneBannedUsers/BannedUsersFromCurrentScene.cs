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
        private readonly bool includeBannedUsersFromScene;

        private CancellationTokenSource checkIfPlayerIsBannedCts;

        public BannedUsersFromCurrentScene(
            IRoomHub roomHub,
            bool includeBannedUsersFromScene)
        {
            this.roomHub = roomHub;
            this.includeBannedUsersFromScene = includeBannedUsersFromScene;
        }

        public bool IsUserBanned(string userId)
        {
            if (!includeBannedUsersFromScene)
                return false;

            return true;

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
