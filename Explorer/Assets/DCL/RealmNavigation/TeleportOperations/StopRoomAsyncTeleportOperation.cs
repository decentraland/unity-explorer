using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.RoomHubs;
using System;
using System.Threading;

namespace DCL.RealmNavigation.TeleportOperations
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

        protected override async UniTask InternalExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            float finalizationProgress =
                teleportParams.LoadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.LiveKitStopping);

            await roomHub.StopLocalRoomsAsync().Timeout(livekitTimeout);
            teleportParams.Report.SetProgress(finalizationProgress);
        }
    }
}
