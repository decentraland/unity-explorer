using Cysharp.Threading.Tasks;
using DCL.Web3.Identities;
using System.Threading;

namespace DCL.Web3.Authenticators
{
    public class ProxyWeb3Authenticator : IWeb3Authenticator
    {
        private readonly IWeb3Authenticator authenticator;
        private readonly IWeb3IdentityCache identityCache;

        public ProxyWeb3Authenticator(
            IWeb3Authenticator authenticator,
            IWeb3IdentityCache identityCache)
        {
            this.authenticator = authenticator;
            this.identityCache = identityCache;
        }

        public void Dispose()
        {
            authenticator.Dispose();
            identityCache.Dispose();
        }

        public async UniTask<IWeb3Identity> LoginAsync(CancellationToken ct)
        {
            IWeb3Identity identity = await authenticator.LoginAsync(ct);
            identityCache.Identity = identity;
            return identity;
        }

        public async UniTask LogoutAsync(CancellationToken cancellationToken)
        {
            await authenticator.LogoutAsync(cancellationToken);
            identityCache.Clear();
        }
    }
}
