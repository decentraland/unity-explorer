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
    public partial class DappWeb3Authenticator
    {
        public class Default : IWeb3VerifiedAuthenticator, IVerifiedEthereumApi
        {
            private readonly IWeb3VerifiedAuthenticator originAuth;
            private readonly IVerifiedEthereumApi originApi;

            public Default(IWeb3IdentityCache identityCache, IDecentralandUrlsSource decentralandUrlsSource, IWeb3AccountFactory web3AccountFactory)
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
                            "eth_call",
                            "eth_blockNumber",
                            "eth_signTypedData_v4",
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
                        "eth_getBlockByNumber",
                        "eth_getCode",
                    },
                    decentralandUrlsSource.Environment,
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

            public void AddVerificationListener(IVerifiedEthereumApi.VerificationDelegate callback) =>
                originApi.AddVerificationListener(callback);

            public UniTask<IWeb3Identity> LoginAsync(CancellationToken ct) =>
                originAuth.LoginAsync(ct);

            public UniTask LogoutAsync(CancellationToken cancellationToken) =>
                originAuth.LogoutAsync(cancellationToken);

            public void SetVerificationListener(IWeb3VerifiedAuthenticator.VerificationDelegate? callback) =>
                originAuth.SetVerificationListener(callback);

            private class InvalidAuthCodeVerificationFeatureFlag : ICodeVerificationFeatureFlag
            {
                public bool ShouldWaitForCodeVerificationFromServer => false;
            }
        }
    }
}
