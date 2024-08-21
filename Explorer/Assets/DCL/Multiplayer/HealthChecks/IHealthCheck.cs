using Cysharp.Threading.Tasks;
using DCL.Multiplayer.HealthChecks.Struct;
using System;
using System.Threading;

namespace DCL.Multiplayer.HealthChecks
{
    public interface IHealthCheck
    {
        UniTask<(bool success, string? errorMessage)> IsRemoteAvailableAsync(CancellationToken ct);

        class AlwaysFails : IHealthCheck
        {
            private readonly string errorMessage;

            public AlwaysFails(string errorMessage)
            {
                this.errorMessage = errorMessage;
            }

            public UniTask<(bool success, string? errorMessage)> IsRemoteAvailableAsync(CancellationToken ct) =>
                UniTask.FromResult((false, errorMessage))!;
        }
    }

    public static class HealthCheckExtensions
    {
        public static IHealthCheck WithRetries(this IHealthCheck origin, int? retriesCount = null, TimeSpan? delayBetweenRetries = null) =>
            new RetriesHealthCheck(origin, retriesCount, delayBetweenRetries);
    }
}
