using Cysharp.Threading.Tasks;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using System.Threading;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class IdentityAnalyticsDecorator : IWeb3VerifiedAuthenticator
    {
        private readonly IWeb3VerifiedAuthenticator core;
        private readonly IAnalyticsController analytics;

        public IdentityAnalyticsDecorator(IWeb3VerifiedAuthenticator core, IAnalyticsController analytics)
        {
            this.core = core;
            this.analytics = analytics;
        }

        public void Dispose()
        {
            core?.Dispose();
        }

        public async UniTask<IWeb3Identity> LoginAsync(CancellationToken ct)
        {
            IWeb3Identity identity = await core.LoginAsync(ct);
            analytics.Identify(identity);
            return identity;
        }

        public async UniTask LogoutAsync(CancellationToken cancellationToken) =>
            await core.LogoutAsync(cancellationToken);

        public void SetVerificationListener(IWeb3VerifiedAuthenticator.VerificationDelegate callback) =>
            core.SetVerificationListener(callback);
    }
}
