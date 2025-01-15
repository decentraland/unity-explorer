using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.RealmNavigation.LoadingOperation;
using System.Threading;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public interface IStartupOperation : ILoadingOperation<IStartupOperation.Params>
    {
        public readonly struct Params : ILoadingOperationParams
        {
            public Params(AsyncLoadProcessReport report)
            {
                Report = report;
            }

            public AsyncLoadProcessReport Report { get; }
        }

        UniTask<EnumResult<TaskError>> ExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct);
    }

    public static class StartupOperation
    {
        public static AnalyticsStartupOperation WithAnalytics(this IStartupOperation operation, IAnalyticsController analyticsController) =>
            new (operation, analyticsController);
    }
}
