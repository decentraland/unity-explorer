#nullable enable

using CodeLess.Attributes;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.LiveKit.Public;
using ECS;
using LiveKit.Rooms;
using System;
using System.Collections.Generic;
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

        private readonly Mutex<HashSet<string>?> bannedAddressesSet = new Mutex<HashSet<string>?>(null); // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
        private readonly Mutex<HashSet<string>?> sceneAdminsAddressesSet = new Mutex<HashSet<string>?>(null); // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG

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

            OnRoomMetadataChanged(roomHub.SceneRoom().Room().Info.Metadata);
        }


#if UNITY_INCLUDE_TESTS
        private RoomMetadataCurrentScene()
        {
        }

        public static void InitializeTest()
        {
            // Idempotent: SingletonRegistry only resets between test assemblies, so consecutive
            // [SetUp] calls in the same fixture would otherwise hit "already initialized".
            try { RoomMetadataCurrentScene.Initialize(new RoomMetadataCurrentScene()); }
            catch (InvalidOperationException) { /* already initialized — fall through and clear state */ }

            RoomMetadataCurrentScene.Instance.SetBannedForTests();
        }

        public void SetBannedForTests(params string[] bannedAddresses)
        {
            using var bannedLock = bannedAddressesSet.Lock(); // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
            bannedLock.Value = bannedAddresses.Length == 0
                ? null
                : new HashSet<string>(bannedAddresses, StringComparer.OrdinalIgnoreCase);
        }
#endif


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

            Result<SceneRoomMetadata> result = SceneRoomMetadata.FromJson(metadata);

            if (result.Success == false)
            {
                ReportHub.LogError(ReportCategory.ENGINE, $"Failed to parse scene room metadata: {result.ErrorMessage}");
                return;
            }

            SceneRoomMetadata usersRoomMetadata = result.Value;

            using var bannedLock = bannedAddressesSet.Lock(); // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
            if (usersRoomMetadata.BannedAddresses is { Length: > 0 })
                bannedLock.Value = new HashSet<string>(usersRoomMetadata.BannedAddresses, StringComparer.OrdinalIgnoreCase);
            else
                bannedLock.Value = null;

            using var adminsLock = sceneAdminsAddressesSet.Lock(); // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
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
            if (roomHub.SceneRoom().Room().Info.ConnectionState != LKConnectionState.ConnConnected)
                return SceneAdminResult.NotLoadedYet();

            if (realmData.IsLocalSceneDevelopment)
                return SceneAdminResult.LocalSceneDevelopment();

            using var adminsLock = sceneAdminsAddressesSet.Lock(); // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
            if (adminsLock.Value == null)
                return SceneAdminResult.NotLoadedYet();

            bool isAdmin = adminsLock.Value.Contains(userId);

            return isAdmin
                ? SceneAdminResult.Success()
                : SceneAdminResult.NotAdmin();
#endif
        }

        public Result<IEnumerable<string>> CurrentAdmins()
        {
            if (roomHub.SceneRoom().Room().Info.ConnectionState != LKConnectionState.ConnConnected)
                return Result<IEnumerable<string>>.ErrorResult("Scene Admins are not available, Livekit room is disconnected");

            if (realmData.IsLocalSceneDevelopment)
                return Result<IEnumerable<string>>.ErrorResult("Scene Admins are not available in Local Scene Development");

            using var adminsLock = sceneAdminsAddressesSet.Lock(); // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
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
#if UNITY_INCLUDE_TESTS
            // Test ctor leaves roomHub null and includeBannedUsersFromScene false.
            // Drive ban state purely through SetBannedForTests so system tests can flip it.
            using var bannedLockTest = bannedAddressesSet.Lock(); // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
            return bannedLockTest.Value?.Contains(userId) ?? false;
#else
            if (!includeBannedUsersFromScene)
                return false;

            if (roomHub.SceneRoom().Room().Info.ConnectionState != LKConnectionState.ConnConnected)
                return false;

            using var bannedLock = bannedAddressesSet.Lock(); // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
            return bannedLock.Value?.Contains(userId) ?? false;
#endif
        }
    }
}
