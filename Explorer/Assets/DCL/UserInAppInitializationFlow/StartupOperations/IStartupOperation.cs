using DCL.AsyncLoadReporting;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.RealmNavigation.LoadingOperation;

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

            public static implicit operator Params(AsyncLoadProcessReport report) =>
                new (report);
        }
    }

    public static class StartupOperation
    {
        public static AnalyticsStartupOperation WithAnalytics(this IStartupOperation operation, IAnalyticsController analyticsController) =>
            new (operation, analyticsController);
    }
}
