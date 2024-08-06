using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.RoomHubs;
using System;
using System.Threading;

namespace DCL.Multiplayer.HealthChecks
{
    public class LivekitHealthCheck : IHealthCheck
    {
        private readonly IRoomHub roomHub;

        public LivekitHealthCheck(IRoomHub roomHub)
        {
            this.roomHub = roomHub;
        }

        public async UniTask<(bool success, string? errorMessage)> IsRemoteAvailableAsync(CancellationToken ct)
        {
            try
            {
                bool result = await roomHub.StartAsync();
                return (result, result ? null : "Cannot connect to livekit rooms");
            }
            catch (Exception) { return (false, "Cannot connect to livekit rooms"); }
        }
    }
}
