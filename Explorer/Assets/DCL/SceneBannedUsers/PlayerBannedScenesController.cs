using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.RealmNavigation;
using ECS.SceneLifeCycle;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.SceneBannedUsers
{
    public class PlayerBannedScenesController
    {
        private readonly IRoomHub roomHub;
        private readonly ECSBannedScene bannedSceneController;
        private readonly ILoadingStatus loadingStatus;

        private CancellationTokenSource setCurrentSceneAsBannedCts;
        private CancellationTokenSource checkIfPlayerIsBannedCts;

        private bool playerIsCurrentlyBanned;

        public PlayerBannedScenesController(
            IRoomHub roomHub,
            ECSBannedScene bannedSceneController,
            ILoadingStatus loadingStatus)
        {
            this.roomHub = roomHub;
            this.bannedSceneController = bannedSceneController;
            this.loadingStatus = loadingStatus;

            roomHub.SceneRoom().CurrentSceneRoomForbiddenAccess += SetCurrentSceneAsBanned;
            roomHub.SceneRoom().CurrentSceneRoomConnected += RestoreCurrentBannedScene_Connected;
            roomHub.SceneRoom().CurrentSceneRoomDisconnected += RestoreCurrentBannedScene_Disconnected;
        }

        public void Dispose()
        {
            roomHub.SceneRoom().CurrentSceneRoomForbiddenAccess -= SetCurrentSceneAsBanned;
            roomHub.SceneRoom().CurrentSceneRoomConnected -= RestoreCurrentBannedScene_Connected;
            roomHub.SceneRoom().CurrentSceneRoomDisconnected -= RestoreCurrentBannedScene_Disconnected;

            setCurrentSceneAsBannedCts.SafeCancelAndDispose();
            checkIfPlayerIsBannedCts.SafeCancelAndDispose();
        }

        private void SetCurrentSceneAsBanned()
        {
            if (playerIsCurrentlyBanned)
                return;

            setCurrentSceneAsBannedCts = setCurrentSceneAsBannedCts.SafeRestart();
            SetCurrentSceneAsBannedAsync(setCurrentSceneAsBannedCts.Token).Forget();
            return;

            async UniTaskVoid SetCurrentSceneAsBannedAsync(CancellationToken ct)
            {
                await UniTask.WaitUntil(() => loadingStatus.CurrentStage.Value == LoadingStatus.LoadingStage.Completed, cancellationToken: ct);
                playerIsCurrentlyBanned = await bannedSceneController.TrySetCurrentSceneAsBannedAsync(ct);
                Debug.Log("SANTI LOG -> BANNED!!");
            }
        }

        private void RestoreCurrentBannedScene_Connected()
        {
            Debug.Log("SANTI LOG -> RESTORE ALL BANNED SCENES (CONNECTED)!!");
            bannedSceneController.RemoveAllBannedSceneComponents();
            playerIsCurrentlyBanned = false;
        }

        private void RestoreCurrentBannedScene_Disconnected()
        {
            Debug.Log("SANTI LOG -> RESTORE ALL BANNED SCENES (DISCONNECTED)!!");
            bannedSceneController.RemoveAllBannedSceneComponents();
            playerIsCurrentlyBanned = false;
        }
    }
}
