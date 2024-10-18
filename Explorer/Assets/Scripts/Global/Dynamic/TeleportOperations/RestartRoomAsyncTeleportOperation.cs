using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.UserInAppInitializationFlow;
using static DCL.UserInAppInitializationFlow.RealFlowLoadingStatus.Stage;
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
                await roomHub.StartAsync().Timeout(livekitTimeout);
                teleportParams.ParentReport.SetProgress(RealFlowLoadingStatus.PROGRESS[Completed]);
                return Result.SuccessResult();
            }
            catch (Exception e)
            {
                return Result.ErrorResult("Cannot restart room");
            }
        }
    }
}