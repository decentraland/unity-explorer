using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.RealmNavigation;
using ECS.SceneLifeCycle;
using System.Threading;
using Utility;

namespace DCL.SceneBannedUsers
{
    /// <summary>
    /// Controls when to unload the current scene when the player is banned from there.
    /// </summary>
    public class PlayerBannedScenesController
    {
        private readonly IRoomHub roomHub;
        private readonly ECSBannedScene bannedSceneController;
        private readonly ILoadingStatus loadingStatus;

        private CancellationTokenSource setCurrentSceneAsBannedCts;

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
        }

        /// <summary>
        /// If we receive a forbidden access response from the server, we mark the current scene to be unloaded.
        /// </summary>
        private void SetCurrentSceneAsBanned()
        {
            setCurrentSceneAsBannedCts = setCurrentSceneAsBannedCts.SafeRestart();
            SetCurrentSceneAsBannedAsync(setCurrentSceneAsBannedCts.Token).Forget();
            return;

            async UniTaskVoid SetCurrentSceneAsBannedAsync(CancellationToken ct)
            {
                if (playerIsCurrentlyBanned)
                    return;

                // By design, we don't want to unload the scene until the loading screen (if exists) has finished.
                await UniTask.WaitUntil(() => loadingStatus.CurrentStage.Value == LoadingStatus.LoadingStage.Completed, cancellationToken: ct);

                playerIsCurrentlyBanned = await bannedSceneController.TrySetCurrentSceneAsBannedAsync(ct);
            }
        }

        /// <summary>
        /// Whenever we successfully connect/disconnect a scene, we un-mark all the existing scenes marked as banned.
        /// </summary>
        private void RestoreCurrentBannedScene()
        {
            bannedSceneController.RemoveAllBannedSceneComponents();
            playerIsCurrentlyBanned = false;
        }
    }
}
