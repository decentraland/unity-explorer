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
#if !UNITY_WEBGL
    public partial class DappWeb3Authenticator
    {
        public class Default : IWeb3Authenticator, IEthereumApi
        {
            private readonly IWeb3Authenticator originAuth;
            private readonly IEthereumApi originApi;

            public Default(IWeb3IdentityCache identityCache, IDecentralandUrlsSource decentralandUrlsSource, IWeb3AccountFactory web3AccountFactory, DecentralandEnvironment environment)
            {
                URLAddress authApiUrl = URLAddress.FromString(decentralandUrlsSource.Url(DecentralandUrl.ApiAuth));
                URLAddress signatureUrl = URLAddress.FromString(decentralandUrlsSource.Url(DecentralandUrl.AuthSignatureWebApp));
                URLDomain rpcServerUrl = URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.ApiRpc));

                var origin = new DappWeb3Authenticator(
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
                    environment,
                    new InvalidAuthCodeVerificationFeatureFlag()
                );

                originApi = origin;
                originAuth = origin;
            }

            public void Dispose()
            {
                originAuth.Dispose(); // Disposes both
            }

            public UniTask<EthApiResponse> SendAsync(EthApiRequest request, CancellationToken ct) =>
                originApi.SendAsync(request, ct);

            public UniTask LogoutAsync(CancellationToken cancellationToken) =>
                originAuth.LogoutAsync(cancellationToken);

            public UniTask<IWeb3Identity> LoginAsync(CancellationToken ct, IWeb3Authenticator.VerificationDelegate? codeVerificationCallback)
            {
                return originAuth.LoginAsync(ct, codeVerificationCallback);
            }

            private class InvalidAuthCodeVerificationFeatureFlag : ICodeVerificationFeatureFlag
            {
                public bool ShouldWaitForCodeVerificationFromServer => false;
            }
        }
    }
#endif
}
