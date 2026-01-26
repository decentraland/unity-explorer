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
        public class Default : IWeb3VerifiedAuthenticator, IEthereumApi
        {
            private readonly IWeb3VerifiedAuthenticator originAuth;
            private readonly IEthereumApi originApi;

            public event Action<(int code, DateTime expiration, string requestId)>? VerificationRequired
            {
                add => originAuth.VerificationRequired += value;
                remove => originAuth.VerificationRequired -= value;
            }

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

            public UniTask<EthApiResponse> SendAsync(EthApiRequest request, Web3RequestSource source, CancellationToken ct) =>
                originApi.SendAsync(request, ct);

            public UniTask<IWeb3Identity> LoginAsync(LoginMethod loginMethod, CancellationToken ct) =>
                originAuth.LoginAsync(loginMethod, ct);

            public UniTask<IWeb3Identity> LoginPayloadedAsync<TPayload>(LoginMethod method, TPayload payload, CancellationToken ct) =>
                originAuth.LoginPayloadedAsync(method, payload, ct);

            public UniTask LogoutAsync(CancellationToken ct) =>
                originAuth.LogoutAsync(ct);

            public void CancelCurrentWeb3Operation()
            {
                originAuth.CancelCurrentWeb3Operation();
            }

            public UniTask SubmitOtp(string otp) =>
                originAuth.SubmitOtp(otp);

            public UniTask ResendOtp() =>
                originAuth.ResendOtp();

            public UniTask<bool> TryAutoConnectAsync(CancellationToken ct) =>
                originAuth.TryAutoConnectAsync(ct);

            private class InvalidAuthCodeVerificationFeatureFlag : ICodeVerificationFeatureFlag
            {
                public bool ShouldWaitForCodeVerificationFromServer => false;
            }
        }
    }
}
