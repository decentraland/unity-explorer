using Cysharp.Threading.Tasks;
using DCL.Multiplayer.HealthChecks.Struct;
using DCL.PerformanceAndDiagnostics.Analytics;
using System.Threading;

namespace DCL.Multiplayer.HealthChecks
{
    public interface IHealthCheck
    {
        UniTask<(bool success, string? errorMessage)> IsRemoteAvailableAsync(CancellationToken ct);
    }

    public static class HealthCheckExtensions
    {
        public static IHealthCheck WithRetries(this IHealthCheck origin, int retriesCount) =>
            new RetriesHealthCheck(origin, retriesCount);

        public static IHealthCheck WithAnalytics(this IHealthCheck origin, IAnalyticsController analyticsController, string eventName) =>
            new AnalyticsHealthCheck(origin, analyticsController, eventName);
    }
}
