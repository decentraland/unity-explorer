using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Abstract;
using DCL.Web3.Identities;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Web3.Authenticators
{
    public partial class DappWeb3Authenticator
    {
        public class Default : IWeb3Authenticator, IEthereumApi, IDappVerificationHandler
        {
            private readonly DappWeb3Authenticator origin;

            public event Action<(int code, DateTime expiration, string requestId)>? VerificationRequired
            {
                add => origin.VerificationRequired += value;
                remove => origin.VerificationRequired -= value;
            }

            public Default(IWeb3IdentityCache identityCache, IDecentralandUrlsSource decentralandUrlsSource, IWeb3AccountFactory web3AccountFactory, DecentralandEnvironment environment)
            {
                URLAddress authApiUrl = URLAddress.FromString(decentralandUrlsSource.Url(DecentralandUrl.ApiAuth));
                URLAddress signatureUrl = URLAddress.FromString(decentralandUrlsSource.Url(DecentralandUrl.AuthSignatureWebApp));
                URLDomain rpcServerUrl = URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.ApiRpc));

                origin = new DappWeb3Authenticator(
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

            // IDappVerificationHandler
            public void CancelCurrentWeb3Operation() =>
                origin.CancelCurrentWeb3Operation();

            private class InvalidAuthCodeVerificationFeatureFlag : ICodeVerificationFeatureFlag
            {
                public bool ShouldWaitForCodeVerificationFromServer => false;
            }
        }
    }
}
