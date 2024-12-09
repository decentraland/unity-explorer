using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.PerformanceAndDiagnostics.Analytics;
using System.Threading;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public interface IStartupOperation
    {
        UniTask<Result> ExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct);
    }

    public static class StartupOperation
    {
        public static AnalyticsStartupOperation WithAnalytics(this IStartupOperation operation, IAnalyticsController analyticsController) =>
            new (operation, analyticsController);
    }
}
