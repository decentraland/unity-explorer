using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Abstract;
using DCL.Web3.Identities;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Web3.Authenticators
{
    public partial class DappWeb3EthereumApi
    {
        public class Default : IWeb3Authenticator, IEthereumApi
        {
            private readonly DappWeb3EthereumApi origin;

            public Default(IWeb3IdentityCache identityCache, IDecentralandUrlsSource decentralandUrlsSource, IWeb3AccountFactory web3AccountFactory, DecentralandEnvironment environment)
            {
                URLAddress authApiUrl = URLAddress.FromString(decentralandUrlsSource.Url(DecentralandUrl.ApiAuth));
                URLAddress signatureUrl = URLAddress.FromString(decentralandUrlsSource.Url(DecentralandUrl.AuthSignatureWebApp));
                URLDomain rpcServerUrl = URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.ApiRpc));

                origin = new DappWeb3EthereumApi(
                    new UnityAppWebBrowser(decentralandUrlsSource),
                    authApiUrl,
                    signatureUrl,
                    rpcServerUrl,
                    identityCache,
                    web3AccountFactory,
                    new HashSet<string>(
                        new[]
                        {
                            "eth_getBalance",
                            "eth_call", "eth_blockNumber", "eth_signTypedData_v4", "eth_sendTransaction"
                        }
                    ),
                    new HashSet<string>
                    {
                        "eth_getTransactionReceipt",
                        "eth_estimateGas",
                        "eth_call",
                        "eth_getBalance",
                        "eth_getStorageAt",
                        "eth_blockNumber",
                        "eth_gasPrice",
                        "eth_protocolVersion",
                        "net_version",
                        "web3_sha3",
                        "web3_clientVersion",
                        "eth_getTransactionCount",
                        "eth_getBlockByNumber", "eth_getCode"
                    },
                    environment
                );
            }

            public void Dispose() =>
                origin.Dispose();

            // IEthereumApi
            public UniTask<EthApiResponse> SendAsync(EthApiRequest request, Web3RequestSource source, CancellationToken ct) =>
                origin.SendAsync(request, source, ct);

            // IWeb3Authenticator
            public UniTask<IWeb3Identity> LoginAsync(LoginPayload payload, CancellationToken ct) =>
                origin.LoginAsync(payload, ct);

            public UniTask LogoutAsync(CancellationToken ct) =>
                origin.LogoutAsync(ct);
        }
    }
}
