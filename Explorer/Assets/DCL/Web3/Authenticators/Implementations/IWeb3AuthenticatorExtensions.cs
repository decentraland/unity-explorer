using DCL.PerformanceAndDiagnostics.Analytics;

namespace DCL.Web3.Authenticators
{
    public static class IWeb3AuthenticatorExtensions
    {
        public static IWeb3Authenticator WithAnalytics(this IWeb3Authenticator authenticator, IAnalyticsController analytics, bool when) =>
            when
                ? new AnalyticsDecoratorAuthenticator(authenticator, analytics)
                : authenticator;
    }
}
