using Cysharp.Threading.Tasks;
using DCL.Multiplayer.HealthChecks;
using DCL.Utility.Types;
using Newtonsoft.Json.Linq;
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

        public async UniTask<Result> IsRemoteAvailableAsync(CancellationToken ct)
        {
            var result = await healthCheck.IsRemoteAvailableAsync(ct);

            if (result.Success == false)
                analytics.Track(
                    EVENT_NAME,
                    new JObject
                    {
                        ["message"] = result.ErrorMessage,
                    }
                );

            return result;
        }
    }
}
