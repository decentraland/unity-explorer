using Cysharp.Threading.Tasks;
using DCL.Multiplayer.HealthChecks.Struct;
using System;
using System.Threading;
using Utility.Types;

namespace DCL.Multiplayer.HealthChecks
{
    public interface IHealthCheck
    {
        UniTask<Result> IsRemoteAvailableAsync(CancellationToken ct);

        class AlwaysFails : IHealthCheck
        {
            private readonly string errorMessage;

            public AlwaysFails(string errorMessage)
            {
                this.errorMessage = errorMessage;
            }

            public UniTask<Result> IsRemoteAvailableAsync(CancellationToken ct) =>
                UniTask.FromResult(Result.ErrorResult(nameof(AlwaysFails)))!;
        }
    }

    public static class HealthCheckExtensions
    {
        public static IHealthCheck WithRetries(this IHealthCheck origin, int? retriesCount = null, TimeSpan? delayBetweenRetries = null) =>
            new RetriesHealthCheck(origin, retriesCount, delayBetweenRetries);
    }
}
