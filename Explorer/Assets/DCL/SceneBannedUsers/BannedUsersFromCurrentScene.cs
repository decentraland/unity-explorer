using CodeLess.Attributes;
using DCL.Multiplayer.Connections.RoomHubs;
using LiveKit.Proto;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
        private string roomMetadata = string.Empty;
        private BannedUsersRoomMetadata bannedUsersRoomMetadata;
        private HashSet<string> bannedAddressesSet;

        public BannedUsersFromCurrentScene(
            IRoomHub roomHub,
            bool includeBannedUsersFromScene)
        {
            this.roomHub = roomHub;
            this.includeBannedUsersFromScene = includeBannedUsersFromScene;
            roomHub.SceneRoom().Room().RoomMetadataChanged += OnRoomMetadataChanged;
        }

        private void OnRoomMetadataChanged(string metadata)
        {
            roomMetadata = metadata;
            bannedUsersRoomMetadata = JsonConvert.DeserializeObject<BannedUsersRoomMetadata>(roomMetadata);

            if (bannedUsersRoomMetadata.BannedAddresses is { Length: > 0 })
                bannedAddressesSet = new HashSet<string>(bannedUsersRoomMetadata.BannedAddresses, StringComparer.OrdinalIgnoreCase);
            else
                bannedAddressesSet = null;
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

            return bannedAddressesSet?.Contains(userId) ?? false;
        }
    }
}
