using CodeLess.Attributes;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Profiles.Self;
using ECS.SceneLifeCycle;
using LiveKit.Proto;
using LiveKit.Rooms;
using Newtonsoft.Json;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.SceneBannedUsers
{
    [Singleton]
    public partial class BannedUsersFromCurrentScene
    {
        private readonly IRoomHub roomHub;
        private readonly ISelfProfile selfProfile;
        private readonly ECSBannedScene bannedSceneController;

        private CancellationTokenSource checkIfPlayerIsBannedCts;

        public BannedUsersFromCurrentScene(
            IRoomHub roomHub,
            ISelfProfile selfProfile,
            ECSBannedScene bannedSceneController)
        {
            this.roomHub = roomHub;
            this.selfProfile = selfProfile;
            this.bannedSceneController = bannedSceneController;

            //roomHub.IslandRoom().ConnectionStateChanged += OnConnectionStateChanged;
            //roomHub.SceneRoom().Room().ConnectionStateChanged += OnConnectionStateChanged;
            roomHub.SceneRoom().Room().ConnectionUpdated += OnConnectionUpdated;
            roomHub.SceneRoom().Room().RoomMetadataChanged += OnRoomMetadataChanged;
        }

        public bool IsUserBanned(string userId)
        {
            if (roomHub.SceneRoom().Room().Info.ConnectionState != ConnectionState.ConnConnected)
                return false;

            Debug.Log($"SANTI LOG -> IsUserBanned() -> ROOM METADATA: [{roomHub.SceneRoom().Room().Info.Metadata}]");

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

        private void OnConnectionStateChanged(ConnectionState connectionState)
        {
            Debug.Log($"SANTI LOG -> connection state changed: [{connectionState.ToString()}]");
            checkIfPlayerIsBannedCts = checkIfPlayerIsBannedCts.SafeRestart();
            CheckIfPlayerIsBannedAsync(checkIfPlayerIsBannedCts.Token).Forget();
        }

        private void OnConnectionUpdated(IRoom room, ConnectionUpdate connectionUpdate)
        {
            Debug.Log($"SANTI LOG -> connection state updated: [{room.Info.Sid}] [{connectionUpdate.ToString()}]");

            if (connectionUpdate == ConnectionUpdate.Connected)
            {
                checkIfPlayerIsBannedCts = checkIfPlayerIsBannedCts.SafeRestart();
                CheckIfPlayerIsBannedAsync(checkIfPlayerIsBannedCts.Token).Forget();
            }
            else
            {
                Debug.Log("SANTI LOG -> ALL BANNED COMPONENTS REMOVED");
                checkIfPlayerIsBannedCts.SafeCancelAndDispose();
                bannedSceneController.RemoveAllBannedComponents();
            }
        }

        private void OnRoomMetadataChanged(string metaData)
        {
            Debug.Log($"SANTI LOG -> OnRoomMetadataChanged: [{metaData}]");
            checkIfPlayerIsBannedCts = checkIfPlayerIsBannedCts.SafeRestart();
            CheckIfPlayerIsBannedAsync(checkIfPlayerIsBannedCts.Token).Forget();
        }

        private async UniTaskVoid CheckIfPlayerIsBannedAsync(CancellationToken ct)
        {
            var ownProfile = await selfProfile.ProfileAsync(ct);
            if (ownProfile == null)
                return;

            if (IsUserBanned(ownProfile.UserId))
            {
                Debug.Log("SANTI LOG -> CheckIfPlayerIsBannedAsync: [BANNED]");
                bannedSceneController.SetCurrentSceneAsBannedAsync(ct).Forget();
            }
        }
    }
}
