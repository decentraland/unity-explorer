using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using System.Threading;

namespace DCL.Multiplayer.Connections.Demo
{
    public static class ArchipelagoFakeIdentityCache
    {
        public static async UniTask<IWeb3IdentityCache> NewAsync(IDecentralandUrlsSource decentralandUrlsSource)
        {
            IWeb3IdentityCache identityCache = new ProxyIdentityCache(
                new MemoryWeb3IdentityCache(),
                new PlayerPrefsIdentityProvider(
                    new PlayerPrefsIdentityProvider.DecentralandIdentityWithNethereumAccountJsonSerializer(),
                    "ArchipelagoTestIdentity"
                )
            );

            if (identityCache.Identity is null)
            {
                IWeb3Identity? identity = await new DappWeb3Authenticator.Default(identityCache, decentralandUrlsSource)
                   .LoginAsync(CancellationToken.None);

                identityCache.Identity = identity;
            }

            identityCache.Identity = new LogWeb3Identity(identityCache.Identity);
            return identityCache;
        }
    }
}
