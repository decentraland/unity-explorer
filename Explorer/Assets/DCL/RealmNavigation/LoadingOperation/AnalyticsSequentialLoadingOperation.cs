using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Utility.Types;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading;

namespace DCL.RealmNavigation.LoadingOperation
{
    /// <summary>
    ///     Reports failed operations to Analytics
    /// </summary>
    /// <typeparam name="TParams"></typeparam>
    public class AnalyticsSequentialLoadingOperation<TParams> : SequentialLoadingOperation<TParams> where TParams: ILoadingOperationParams
    {
        private readonly IAnalyticsController analyticsController;
        private readonly string reportedOpType;

        public AnalyticsSequentialLoadingOperation(
            ILoadingStatus loadingStatus,
            IReadOnlyList<ILoadingOperation<TParams>> operations,
            ReportData reportData,
            IAnalyticsController analyticsController,
            string reportedOpType) : base(loadingStatus, operations, reportData)
        {
            this.analyticsController = analyticsController;
            this.reportedOpType = reportedOpType;
        }

        public override async UniTask<EnumResult<TaskError>> ExecuteAsync(string processName, int attemptsCount, TParams args, CancellationToken ct)
        {
            EnumResult<TaskError> result = await base.ExecuteAsync(processName, attemptsCount, args, ct);

            if (result.Success == false)
                analyticsController.Track(
                    AnalyticsEvents.General.LOADING_ERROR,
                    new JObject
                    {
                        ["type"] = reportedOpType,
                        ["message"] = result.AsResult().ErrorMessage,
                    }
                );

            return result;
        }
    }
}
