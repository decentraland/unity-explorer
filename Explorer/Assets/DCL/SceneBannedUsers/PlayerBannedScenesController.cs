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
            roomHub.SceneRoom().CurrentSceneRoomConnected += RestoreCurrentBannedScene;
            roomHub.SceneRoom().CurrentSceneRoomDisconnected += RestoreCurrentBannedScene;
        }

        public void Dispose()
        {
            roomHub.SceneRoom().CurrentSceneRoomForbiddenAccess -= SetCurrentSceneAsBanned;
            roomHub.SceneRoom().CurrentSceneRoomConnected -= RestoreCurrentBannedScene;
            roomHub.SceneRoom().CurrentSceneRoomDisconnected -= RestoreCurrentBannedScene;

            setCurrentSceneAsBannedCts.SafeCancelAndDispose();
            checkIfPlayerIsBannedCts.SafeCancelAndDispose();
        }

        private void SetCurrentSceneAsBanned()
        {
            setCurrentSceneAsBannedCts = setCurrentSceneAsBannedCts.SafeRestart();
            SetCurrentSceneAsBannedAsync(setCurrentSceneAsBannedCts.Token).Forget();
            return;

            async UniTaskVoid SetCurrentSceneAsBannedAsync(CancellationToken ct)
            {
                if (playerIsCurrentlyBanned)
                    return;

                await UniTask.WaitUntil(() => loadingStatus.CurrentStage.Value == LoadingStatus.LoadingStage.Completed, cancellationToken: ct);
                playerIsCurrentlyBanned = await bannedSceneController.TrySetCurrentSceneAsBannedAsync(ct);
                Debug.Log("SANTI LOG -> BANNED!!");
            }
        }

        private void RestoreCurrentBannedScene()
        {
            Debug.Log("SANTI LOG -> RESTORE ALL BANNED SCENES!!");
            bannedSceneController.RemoveAllBannedSceneComponents();
            playerIsCurrentlyBanned = false;
        }
    }
}
