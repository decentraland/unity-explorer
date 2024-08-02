using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.RoomHubs;
using System.Threading;

namespace DCL.Multiplayer.HealthChecks.Livekit
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
            bool result = await roomHub.StartAsync();
            return (result, null);
        }
    }
}
