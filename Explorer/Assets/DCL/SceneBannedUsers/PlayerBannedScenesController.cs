using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.RoomHubs;
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

        private CancellationTokenSource setCurrentSceneAsBannedCts;
        private CancellationTokenSource checkIfPlayerIsBannedCts;

        private bool playerIsCurrentlyBanned;

        public PlayerBannedScenesController(
            IRoomHub roomHub,
            ECSBannedScene bannedSceneController)
        {
            this.roomHub = roomHub;
            this.bannedSceneController = bannedSceneController;

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
