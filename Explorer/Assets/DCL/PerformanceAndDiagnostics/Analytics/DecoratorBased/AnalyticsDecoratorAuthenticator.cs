using Cysharp.Threading.Tasks;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using System.Threading;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class AnalyticsDecoratorAuthenticator : IWeb3Authenticator
    {
        private readonly IWeb3Authenticator core;
        private readonly IAnalyticsController analytics;

        public AnalyticsDecoratorAuthenticator(IWeb3Authenticator core, IAnalyticsController analytics)
        {
            this.core = core;
            this.analytics = analytics;
        }

        public void Dispose()
        {
            core?.Dispose();
        }

        public async UniTask<IWeb3Identity> LoginAsync(string email, CancellationToken ct)
        {
            IWeb3Identity identity = await core.LoginAsync(email, ct);
            analytics.Identify(identity);
            return identity;
        }

        public async UniTask LogoutAsync(CancellationToken cancellationToken) =>
            await core.LogoutAsync(cancellationToken);
    }
}
