using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.UserInAppInitializationFlow;
using Utility.Types;

namespace Global.Dynamic.TeleportOperations
{
    public class StopRoomAsyncTeleportOperation : TeleportOperationBase
    {
        private readonly IRoomHub roomHub;
        private readonly TimeSpan livekitTimeout;

        public StopRoomAsyncTeleportOperation(IRoomHub roomHub, TimeSpan livekitTimeout)
        {
            this.roomHub = roomHub;
            this.livekitTimeout = livekitTimeout;
        }

        protected override async UniTask ExecuteAsyncInternal(TeleportParams teleportParams, CancellationToken ct)
        {
            float finalizationProgress =
                teleportParams.LoadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.LivekitStopping);

            await roomHub.StopIfNotAsync().Timeout(livekitTimeout);
            teleportParams.ParentReport.SetProgress(finalizationProgress);
        }
    }
}
