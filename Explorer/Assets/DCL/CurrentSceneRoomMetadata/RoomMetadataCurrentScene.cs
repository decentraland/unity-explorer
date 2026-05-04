using CodeLess.Attributes;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.LiveKit.Public;
using ECS;
using LiveKit.Proto;
using LiveKit.Rooms;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility.Multithreading;
using RichTypes;
using REnum;

namespace DCL.SceneBannedUsers
{
    [REnum]
    [REnumFieldEmpty("Success")]
    [REnumFieldEmpty("NotAdmin")]
    [REnumFieldEmpty("NotLoadedYet")]
    [REnumFieldEmpty("LocalSceneDevelopment")]
    public partial struct SceneAdminResult
    {
    }

    /// <summary>
    /// Singleton class to check is a user is banned from the current scene.
    /// </summary>
    [Singleton]
    public partial class RoomMetadataCurrentScene
    {
        private readonly IRoomHub roomHub;
        private readonly IRealmData realmData;
        private readonly bool includeBannedUsersFromScene;

        private CancellationTokenSource checkIfPlayerIsBannedCts;
        private string roomMetadata = string.Empty;
        private SceneRoomMetadata usersRoomMetadata;
        private HashSet<string>? bannedAddressesSet;

        private Mutex<HashSet<string>?> sceneAdminsAddressesSet = new Mutex<HashSet<string>?>(null);

        public RoomMetadataCurrentScene(
            IRoomHub roomHub,
            IRealmData realmData,
            bool includeBannedUsersFromScene)
        {
            this.roomHub = roomHub;
            this.realmData = realmData;
            this.includeBannedUsersFromScene = includeBannedUsersFromScene;
            roomHub.SceneRoom().Room().RoomMetadataChanged += OnRoomMetadataChanged;
            roomHub.SceneRoom().Room().ConnectionUpdated += OnConnectionUpdated;
        }

        private void OnConnectionUpdated(IRoom room, ConnectionUpdate connectionUpdate, LKDisconnectReason? disconnectReason)
        {
            if (connectionUpdate is ConnectionUpdate.Connected or ConnectionUpdate.Reconnected)
                OnRoomMetadataChanged(room.Info.Metadata);
        }

        private void OnRoomMetadataChanged(string metadata)
        {
            UpdateBannedList(metadata);
        }

        private void UpdateBannedList(string metadata)
        {
            if (string.IsNullOrEmpty(metadata)) return;

            roomMetadata = metadata;
            usersRoomMetadata = JsonConvert.DeserializeObject<SceneRoomMetadata>(roomMetadata);

            if (usersRoomMetadata.BannedAddresses is { Length: > 0 })
                bannedAddressesSet = new HashSet<string>(usersRoomMetadata.BannedAddresses, StringComparer.OrdinalIgnoreCase);
            else
                bannedAddressesSet = null;

            using var adminsLock = sceneAdminsAddressesSet.Lock();
            if (usersRoomMetadata.SceneAdmins is { Length: > 0 })
                adminsLock.Value = new HashSet<string>(usersRoomMetadata.SceneAdmins, StringComparer.OrdinalIgnoreCase);
            else
                adminsLock.Value = null;
        }

        public SceneAdminResult IsAdmin(string userId)
        {
#if UNITY_INCLUDE_TESTS
            return SceneAdminResult.Success(); // consider always an admin during tests
#else
            if (realmData.IsLocalSceneDevelopment)
                return SceneAdminResult.LocalSceneDevelopment();

            using var adminsLock = sceneAdminsAddressesSet.Lock();
            if (adminsLock.Value == null)
            {
                return SceneAdminResult.NotLoadedYet();
            }

            bool isAdmin = adminsLock.Value.Contains(userId);

            if (isAdmin)
            {
                return SceneAdminResult.Success();
            }
            else
            {
                return SceneAdminResult.NotAdmin();
            }
#endif
        }

        public Result<IEnumerable<string>> CurrentAdmins()
        {
            if (realmData.IsLocalSceneDevelopment)
                return Result<IEnumerable<string>>.ErrorResult("Scene Admins are not available in Local Scene Development");

            using var adminsLock = sceneAdminsAddressesSet.Lock();
            if (adminsLock.Value != null)
            {
                return Result<IEnumerable<string>>.SuccessResult(adminsLock.Value);
            }
            else
            {
                return Result<IEnumerable<string>>.ErrorResult($"Cannot provide admins. Initial load is not finished yet");
            }
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

            if (roomHub.SceneRoom().Room().Info.ConnectionState != LKConnectionState.ConnConnected)
                return false;

            return bannedAddressesSet?.Contains(userId) ?? false;
        }
    }
}
