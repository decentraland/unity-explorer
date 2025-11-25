using CodeLess.Attributes;
using DCL.Multiplayer.Connections.RoomHubs;
using LiveKit.Proto;
using Newtonsoft.Json;
using System;
using System.Threading;

namespace DCL.SceneBannedUsers
{
    /// <summary>
    /// Singleton class to check is a user is banned from the current scene.
    /// </summary>
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

        /// <summary>
        /// Checks if a user is banned from the current scene.
        /// </summary>
        /// <param name="userId">Wallet address.</param>
        /// <returns>True is the user is currently banned from the current scene.</returns>
        public bool IsUserBanned(string userId)
        {
            if (!includeBannedUsersFromScene)
                return false;

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

        /// <summary>
        /// Returns the number of banned users from the current scene.
        /// </summary>
        /// <returns>Number of banned users from the current scene.</returns>
        public int BannedUsersCount()
        {
            if (!includeBannedUsersFromScene)
                return 0;

            if (roomHub.SceneRoom().Room().Info.ConnectionState != ConnectionState.ConnConnected)
                return 0;

            string roomMetadata = roomHub.SceneRoom().Room().Info.Metadata;

            if (string.IsNullOrEmpty(roomMetadata))
                return 0;

            BannedUsersRoomMetadata bannedUsersRoomMetadata = JsonConvert.DeserializeObject<BannedUsersRoomMetadata>(roomMetadata);

            return bannedUsersRoomMetadata.bannedAddresses?.Length ?? 0;
        }
    }
}
