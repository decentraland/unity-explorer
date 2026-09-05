using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Utility.Types;
using System;
using System.Threading;

namespace DCL.Multiplayer.HealthChecks
{
    public class StartLiveKitRooms : IHealthCheck
    {
        private readonly IRoomHub roomHub;

        public StartLiveKitRooms(IRoomHub roomHub)
        {
            this.roomHub = roomHub;
        }

        public async UniTask<Result> IsRemoteAvailableAsync(CancellationToken ct)
        {
            try
            {
                bool result = await roomHub.StartAsync();
                return result ? Result.SuccessResult() : CannotConnectResult();
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.LIVEKIT);
                return CannotConnectResult();
            }
        }

        private Result CannotConnectResult() =>
            Result.ErrorResult($"Cannot connect to livekit rooms: {roomHub.RoomsStateInfo()}");
    }
}
