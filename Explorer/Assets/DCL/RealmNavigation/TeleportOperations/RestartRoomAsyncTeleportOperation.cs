using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.RoomHubs;
using System;
using System.Threading;

namespace DCL.RealmNavigation.TeleportOperations
{
    public class RestartRoomAsyncTeleportOperation : TeleportOperationBase
    {
        private readonly TimeSpan livekitTimeout;
        private readonly IRoomHub roomHub;

        public RestartRoomAsyncTeleportOperation(IRoomHub roomHub, TimeSpan livekitTimeout)
        {
            this.roomHub = roomHub;
            this.livekitTimeout = livekitTimeout;
        }

        protected override async UniTask InternalExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            float finalizationProgress =
                teleportParams.LoadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.LivekitRestarting);

            try
            {
                await roomHub.StartAsync().Timeout(livekitTimeout);
            }
            catch (TimeoutException) { ReportHub.Log(ReportCategory.LIVEKIT, "Livekit handshake timed out; proceeding with teleport"); }

            teleportParams.Report.SetProgress(finalizationProgress);
        }
    }
}
