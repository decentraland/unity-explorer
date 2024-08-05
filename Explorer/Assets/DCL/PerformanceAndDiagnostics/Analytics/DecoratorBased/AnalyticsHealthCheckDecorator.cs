using Cysharp.Threading.Tasks;
using DCL.Multiplayer.HealthChecks;
using Segment.Serialization;
using System.Threading;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class AnalyticsHealthCheckDecorator : IHealthCheck
    {
        private readonly IHealthCheck healthCheck;
        private readonly IAnalyticsController analytics;
        private readonly string eventName;

        public AnalyticsHealthCheckDecorator(IHealthCheck healthCheck, IAnalyticsController analytics, string eventName)
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
