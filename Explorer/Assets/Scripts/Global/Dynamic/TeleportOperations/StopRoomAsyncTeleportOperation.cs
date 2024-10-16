using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.UserInAppInitializationFlow;
using Utility.Types;

namespace Global.Dynamic.TeleportOperations
{
    public class StopRoomAsyncTeleportOperation : ITeleportOperation
    {
        private readonly IRoomHub roomHub;
        private readonly TimeSpan livekitTimeout;

        public StopRoomAsyncTeleportOperation(IRoomHub roomHub, TimeSpan livekitTimeout)
        {
            this.roomHub = roomHub;
            this.livekitTimeout = livekitTimeout;
        }


        public async UniTask<Result> ExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            try
            {
                teleportParams.LoadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.LivekitStopping);
                await roomHub.StopIfNotAsync().Timeout(livekitTimeout);
                return Result.SuccessResult();
            }
            catch (Exception e)
            {
                return Result.ErrorResult("Cannot stop room");
            }
        }
    }
}