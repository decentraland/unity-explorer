using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.Multiplayer.HealthChecks.Struct
{
    public class RetriesHealthCheck : IHealthCheck
    {
        private readonly IHealthCheck origin;
        private readonly int retriesCount;
        private readonly TimeSpan delayBetweenRetries;

        private const int DEFAULT_RETRIES_COUNT = 3;
        private static readonly TimeSpan DEFAULT_DELAY_BETWEEN_RETRIES = TimeSpan.FromSeconds(1);

        public RetriesHealthCheck(IHealthCheck origin, int? retriesCount = null, TimeSpan? delayBetweenRetries = null)
        {
            this.origin = origin;
            this.retriesCount = retriesCount ?? DEFAULT_RETRIES_COUNT;
            this.delayBetweenRetries = delayBetweenRetries ?? DEFAULT_DELAY_BETWEEN_RETRIES;
        }

        public async UniTask<(bool success, string? errorMessage)> IsRemoteAvailableAsync(CancellationToken ct)
        {
            (bool success, string? errorMessage) result = (false, null);

            for (var i = 0; i < retriesCount; i++)
            {
                result = await origin.IsRemoteAvailableAsync(ct);
                if (result.success) break;
                await UniTask.Delay(delayBetweenRetries, cancellationToken: ct);
            }

            return result;
        }
    }
}
