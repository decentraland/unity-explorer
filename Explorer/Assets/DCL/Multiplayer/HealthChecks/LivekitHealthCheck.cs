using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Utilities.Extensions;
using System;
using System.Threading;
using Utility.Types;

namespace DCL.Multiplayer.HealthChecks
{
    public class LivekitHealthCheck : IHealthCheck
    {
        private readonly IRoomHub roomHub;

        public LivekitHealthCheck(IRoomHub roomHub)
        {
            this.roomHub = roomHub;
        }

        public async UniTask<Result> IsRemoteAvailableAsync(CancellationToken ct)
        {
            try
            {
                bool result = await roomHub.StartAsync();
                return result ? Result.SuccessResult() : Result.ErrorResult("Cannot connect to livekit rooms");
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.LIVEKIT);
                return Result.ErrorResult("Cannot connect to livekit rooms");
            }
        }
    }
}
