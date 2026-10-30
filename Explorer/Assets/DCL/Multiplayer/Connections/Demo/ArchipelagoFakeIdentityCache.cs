using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Abstract;
using DCL.Web3.Authenticators;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using System.Threading;

namespace DCL.Multiplayer.Connections.Demo
{
    public static class ArchipelagoFakeIdentityCache
    {
        public static async UniTask<IWeb3IdentityCache> NewAsync(
            IDecentralandUrlsSource decentralandUrlsSource,
            IWeb3AccountFactory web3AccountFactory,
            EthereumNetwork ethereumNetwork
        )
        {
            IWeb3IdentityCache identityCache = new ProxyIdentityCache(
                new MemoryWeb3IdentityCache(),
                new PlayerPrefsIdentityProvider(
                    new PlayerPrefsIdentityProvider.DecentralandIdentityWithNethereumAccountJsonSerializer(
                        web3AccountFactory
                    ),
                    ethereumNetwork
                )
            );

            if (identityCache.Identity is null)
            {
                IWeb3Identity? identity = await new DappWeb3EthereumApi.Default(identityCache, decentralandUrlsSource, web3AccountFactory, ethereumNetwork)
                   .LoginAsync(LoginPayload.ForDappFlow(LoginMethod.ANY), CancellationToken.None);

                identityCache.Identity = identity;
            }

            identityCache.Identity = new LogWeb3Identity(identityCache.Identity);
            return identityCache;
        }
    }
}
