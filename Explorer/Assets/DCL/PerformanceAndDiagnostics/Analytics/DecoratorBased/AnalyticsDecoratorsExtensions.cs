using DCL.Multiplayer.HealthChecks;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public static class AnalyticsDecoratorsExtensions
    {
        public static IHealthCheck WithFailAnalytics(this IHealthCheck origin, IAnalyticsController analyticsController) =>
            new FailAnalyticsHealthCheckDecorator(origin, analyticsController);
    }
}
