using CodeLess.Attributes;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Profiles.Self;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Realm;
using LiveKit.Proto;
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
        private readonly IRealmNavigator realmNavigator;
        private readonly ECSReloadScene reloadScene;
        private readonly IScenesCache scenesCache;

        private CancellationTokenSource checkIfPlayerIsBannedCts;

        public BannedUsersFromCurrentScene(
            IRoomHub roomHub,
            ISelfProfile selfProfile,
            IRealmNavigator realmNavigator,
            ECSReloadScene reloadScene,
            IScenesCache scenesCache)
        {
            this.roomHub = roomHub;
            this.selfProfile = selfProfile;
            this.realmNavigator = realmNavigator;
            this.reloadScene = reloadScene;
            this.scenesCache = scenesCache;

            roomHub.SceneRoom().Room().ConnectionStateChanged += OnConnectionStateChanged;
            roomHub.SceneRoom().Room().RoomMetadataChanged += OnRoomMetadataChanged;
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

        private void OnConnectionStateChanged(ConnectionState connectionState)
        {
            Debug.Log($"SANTI LOG -> OnConnectionStateChanged: [{connectionState.ToString()}]");
            checkIfPlayerIsBannedCts = checkIfPlayerIsBannedCts.SafeRestart();
            CheckIfPlayerIsBannedAsync(checkIfPlayerIsBannedCts.Token).Forget();
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

            Debug.Log("SANTI LOG -> CheckIfPlayerIsBannedAsync: [NOT BANNED]");
            reloadScene.SetSceneAsUnbannedAsync();

            if (IsUserBanned(ownProfile.UserId))
            {
                //realmNavigator.TeleportToParcelAsync(Vector2Int.zero, ct, false).Forget();

                Debug.Log("SANTI LOG -> CheckIfPlayerIsBannedAsync: [BANNED]");
                reloadScene.SetSceneAsBannedAsync(ct).Forget();
            }
        }
    }
}
