using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Web3.Identities;

namespace DCL.Web3.Authenticators
{
    public static class IWeb3AuthenticatorExtensions
    {
        public static IWeb3Authenticator WithIdentityCache(this IWeb3Authenticator authenticator, IWeb3IdentityCache identityCache) =>
            new ProxyWeb3Authenticator(authenticator, identityCache);

        public static IWeb3Authenticator WithAnalytics(this IWeb3Authenticator authenticator, IAnalyticsController analytics, bool when) =>
            when
                ? new AnalyticsDecoratorAuthenticator(authenticator, analytics)
                : authenticator;
    }
}
