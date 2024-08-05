using Cysharp.Threading.Tasks;
using DCL.PerformanceAndDiagnostics.Analytics;
using Segment.Serialization;
using System.Threading;

namespace DCL.Multiplayer.HealthChecks
{
    public class AnalyticsHealthCheck : IHealthCheck
    {
        private readonly IHealthCheck healthCheck;
        private readonly IAnalyticsController analytics;
        private readonly string eventName;

        public AnalyticsHealthCheck(IHealthCheck healthCheck, IAnalyticsController analytics, string eventName)
        {
            this.healthCheck = healthCheck;
            this.analytics = analytics;
            this.eventName = eventName;
        }

        public async UniTask<(bool success, string? errorMessage)> IsRemoteAvailableAsync(CancellationToken ct)
        {
            var result = await healthCheck.IsRemoteAvailableAsync(ct);

            if (result.success == false)
                analytics.Track(
                    eventName,
                    new JsonObject
                    {
                        ["message"] = result.errorMessage
                    }
                );

            return result;
        }
    }
}
