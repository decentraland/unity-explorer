using Cysharp.Threading.Tasks;
using DCL.Multiplayer.HealthChecks;
using Segment.Serialization;
using System.Threading;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class FailAnalyticsHealthCheckDecorator : IHealthCheck
    {
        private readonly IHealthCheck healthCheck;
        private readonly IAnalyticsController analytics;
        private const string EVENT_NAME = AnalyticsEvents.Livekit.LIVEKIT_HEALTH_CHECK_FAILED;

        public FailAnalyticsHealthCheckDecorator(IHealthCheck healthCheck, IAnalyticsController analytics)
        {
            this.healthCheck = healthCheck;
            this.analytics = analytics;
        }

        public async UniTask<(bool success, string? errorMessage)> IsRemoteAvailableAsync(CancellationToken ct)
        {
            var result = await healthCheck.IsRemoteAvailableAsync(ct);

            if (result.success == false)
                analytics.Track(
                    EVENT_NAME,
                    new JsonObject
                    {
                        ["message"] = result.errorMessage,
                    }
                );

            return result;
        }
    }
}
