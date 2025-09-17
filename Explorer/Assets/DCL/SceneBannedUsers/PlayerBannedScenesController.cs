using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Profiles.Self;
using DCL.RealmNavigation;
using ECS.SceneLifeCycle;
using LiveKit.Proto;
using LiveKit.Rooms;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.SceneBannedUsers
{
    public class PlayerBannedScenesController
    {
        private readonly IRoomHub roomHub;
        private readonly ISelfProfile selfProfile;
        private readonly ECSBannedScene bannedSceneController;
        private readonly ILoadingStatus loadingStatus;

        private CancellationTokenSource checkIfPlayerIsBannedCts;

        public PlayerBannedScenesController(
            IRoomHub roomHub,
            ISelfProfile selfProfile,
            ECSBannedScene bannedSceneController,
            ILoadingStatus loadingStatus)
        {
            this.roomHub = roomHub;
            this.selfProfile = selfProfile;
            this.bannedSceneController = bannedSceneController;
            this.loadingStatus = loadingStatus;

            //roomHub.IslandRoom().ConnectionStateChanged += OnConnectionStateChanged;
            //roomHub.SceneRoom().Room().ConnectionStateChanged += OnConnectionStateChanged;
            roomHub.SceneRoom().Room().ConnectionUpdated += OnConnectionUpdated;
            roomHub.SceneRoom().Room().RoomMetadataChanged += OnRoomMetadataChanged;
        }

        public void Dispose()
        {
            //roomHub.IslandRoom().ConnectionStateChanged -= OnConnectionStateChanged;
            //roomHub.SceneRoom().Room().ConnectionStateChanged -= OnConnectionStateChanged;
            roomHub.SceneRoom().Room().ConnectionUpdated -= OnConnectionUpdated;
            roomHub.SceneRoom().Room().RoomMetadataChanged -= OnRoomMetadataChanged;
        }

        private void OnConnectionStateChanged(ConnectionState connectionState)
        {
            Debug.Log($"SANTI LOG -> connection state changed: [{connectionState.ToString()}]");

            if (connectionState == ConnectionState.ConnConnected)
            {
                checkIfPlayerIsBannedCts = checkIfPlayerIsBannedCts.SafeRestart();
                CheckIfPlayerIsBannedAsync(checkIfPlayerIsBannedCts.Token).Forget();
            }
            else
            {
                Debug.Log("SANTI LOG -> ALL BANNED SCENE COMPONENTS REMOVED!!");
                checkIfPlayerIsBannedCts.SafeCancelAndDispose();
                bannedSceneController.RemoveAllBannedSceneComponents();
            }
        }

        private void OnConnectionUpdated(IRoom room, ConnectionUpdate connectionUpdate, DisconnectReason? disconnectReason)
        {
            Debug.Log($"SANTI LOG -> connection state updated: [{room.Info.Sid}] [{connectionUpdate.ToString()}]");

            if (connectionUpdate == ConnectionUpdate.Connected)
            {
                checkIfPlayerIsBannedCts = checkIfPlayerIsBannedCts.SafeRestart();
                CheckIfPlayerIsBannedAsync(checkIfPlayerIsBannedCts.Token).Forget();
            }
            else
            {
                Debug.Log("SANTI LOG -> ALL BANNED SCENE COMPONENTS REMOVED!!");
                checkIfPlayerIsBannedCts.SafeCancelAndDispose();
                bannedSceneController.RemoveAllBannedSceneComponents();
            }
        }

        private void OnRoomMetadataChanged(string metaData)
        {
            Debug.Log($"SANTI LOG -> room metadata changed: [{metaData}]");
            checkIfPlayerIsBannedCts = checkIfPlayerIsBannedCts.SafeRestart();
            CheckIfPlayerIsBannedAsync(checkIfPlayerIsBannedCts.Token).Forget();
        }

        private async UniTaskVoid CheckIfPlayerIsBannedAsync(CancellationToken ct)
        {
            var ownProfile = await selfProfile.ProfileAsync(ct);
            if (ownProfile == null)
                return;

            await UniTask.WaitUntil(() => loadingStatus.CurrentStage.Value == LoadingStatus.LoadingStage.Completed, cancellationToken: ct);

            if (BannedUsersFromCurrentScene.Instance.IsUserBanned(ownProfile.UserId))
            {
                Debug.Log("SANTI LOG -> CheckIfPlayerIsBannedAsync: [TRYING TO BAN]");
                bannedSceneController.TrySetCurrentSceneAsBannedAsync(ct).Forget();
            }
        }
    }
}
