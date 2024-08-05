using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.Multiplayer.HealthChecks.Struct
{
    public class RetriesHealthCheck : IHealthCheck
    {
        private readonly IHealthCheck origin;
        private readonly int retriesCount;

        public RetriesHealthCheck(IHealthCheck origin, int retriesCount)
        {
            this.origin = origin;
            this.retriesCount = retriesCount;
        }

        public async UniTask<(bool success, string? errorMessage)> IsRemoteAvailableAsync(CancellationToken ct)
        {
            (bool success, string? errorMessage) result = (false, null);

            for (var i = 0; i < retriesCount; i++)
            {
                result = await origin.IsRemoteAvailableAsync(ct);
                if (result.success) break;
            }

            return result;
        }
    }
}
