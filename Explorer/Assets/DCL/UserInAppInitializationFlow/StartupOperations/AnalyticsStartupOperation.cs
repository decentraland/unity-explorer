using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Diagnostics;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.RealmNavigation;
using DCL.RealmNavigation.LoadingOperation;
using Segment.Serialization;
using System.Collections.Generic;
using System.Threading;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class AnalyticsStartupOperation : SequentialLoadingOperation<IStartupOperation.Params>
    {
        private readonly IAnalyticsController analyticsController;

        public AnalyticsStartupOperation(
            IAnalyticsController analyticsController,
            ILoadingStatus loadingStatus,
            IReadOnlyList<ILoadingOperation<IStartupOperation.Params>> operations, ReportData reportData)
            : base(loadingStatus, operations, reportData)
        {
            this.analyticsController = analyticsController;
        }

        public override async UniTask<EnumResult<TaskError>> ExecuteAsync(string processName, int attemptsCount, IStartupOperation.Params args, CancellationToken ct)
        {
            EnumResult<TaskError> result = await base.ExecuteAsync(processName, attemptsCount, args, ct);

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
