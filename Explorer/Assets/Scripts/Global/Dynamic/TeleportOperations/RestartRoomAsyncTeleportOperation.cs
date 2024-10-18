using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.UserInAppInitializationFlow;
using Utility.Types;

namespace Global.Dynamic.TeleportOperations
{
    public class RestartRoomAsyncTeleportOperation : ITeleportOperation
    {
        private readonly TimeSpan livekitTimeout;
        private readonly IRoomHub roomHub;

        public RestartRoomAsyncTeleportOperation(IRoomHub roomHub, TimeSpan livekitTimeout)
        {
            this.roomHub = roomHub;
            this.livekitTimeout = livekitTimeout;
        }

        public async UniTask<Result> ExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            try
            {
                float finalizationProgress =
                    teleportParams.LoadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.LivekitRestarting);
                await roomHub.StartAsync().Timeout(livekitTimeout);
                teleportParams.ParentReport.SetProgress(finalizationProgress);
                return Result.SuccessResult();
            }
            catch (Exception e)
            {
                return Result.ErrorResult("Cannot restart room");
            }
        }
    }
}