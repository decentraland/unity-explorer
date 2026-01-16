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
UnityEngine.Debug.Log("ProxyWeb3Authenticator.cs:28"); // SPECIAL_DEBUG_LINE_STATEMENT
            IWeb3Identity identity = await authenticator.LoginAsync(ct);
UnityEngine.Debug.Log("ProxyWeb3Authenticator.cs:30"); // SPECIAL_DEBUG_LINE_STATEMENT
            identityCache.Identity = identity;
UnityEngine.Debug.Log("ProxyWeb3Authenticator.cs:32"); // SPECIAL_DEBUG_LINE_STATEMENT
            return identity;
        }

        public async UniTask LogoutAsync(CancellationToken cancellationToken)
        {
UnityEngine.Debug.Log("ProxyWeb3Authenticator.cs:38"); // SPECIAL_DEBUG_LINE_STATEMENT
            await authenticator.LogoutAsync(cancellationToken);
UnityEngine.Debug.Log("ProxyWeb3Authenticator.cs:40"); // SPECIAL_DEBUG_LINE_STATEMENT
            identityCache.Clear();
UnityEngine.Debug.Log("ProxyWeb3Authenticator.cs:42"); // SPECIAL_DEBUG_LINE_STATEMENT
        }
    }
}
