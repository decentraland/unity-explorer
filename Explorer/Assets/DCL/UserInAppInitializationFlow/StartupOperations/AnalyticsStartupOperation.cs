using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.PerformanceAndDiagnostics.Analytics;
using Segment.Serialization;
using System.Threading;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class AnalyticsStartupOperation : IStartupOperation
    {
        private readonly IStartupOperation origin;
        private readonly IAnalyticsController analyticsController;

        public AnalyticsStartupOperation(IStartupOperation origin, IAnalyticsController analyticsController)
        {
            this.origin = origin;
            this.analyticsController = analyticsController;
        }

        public async UniTask<EnumResult<TaskError>> ExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            var result = await origin.ExecuteAsync(report, ct);

            if (result.Success == false)
                analyticsController.Track(
                    AnalyticsEvents.General.LOADING_ERROR,
                    new JsonObject
                    {
                        ["type"] = "start-up",
                        ["message"] = result.AsResult().ErrorMessage,
                    }
                );

            return result;
        }
    }
}
