using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Abstract;
using DCL.Web3.Identities;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Web3.Authenticators
{
    public partial class ThirdWebAuthenticator
    {
        public class Default : IWeb3VerifiedAuthenticator, IVerifiedEthereumApi
        {
            private readonly IWeb3VerifiedAuthenticator originAuth;
            private readonly IVerifiedEthereumApi originApi;

            public Default(IWeb3IdentityCache identityCache, DecentralandEnvironment environment, IWeb3AccountFactory web3AccountFactory)
            {
                var origin = new ThirdWebAuthenticator(
                    environment,
                    identityCache,
                    new HashSet<string>
                    {
                        // Transaction methods
                        "eth_sendTransaction",

                        // Read methods
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

                        // Wallet/signature methods
                        "eth_requestAccounts",
                        "eth_accounts",
                        "eth_chainId",
                        "personal_sign",
                        "eth_signTypedData_v4",
                    },
                    web3AccountFactory
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

            public UniTask<IWeb3Identity> LoginAsync(string email, CancellationToken ct) =>
                originAuth.LoginAsync(email, ct);

            public UniTask LogoutAsync(CancellationToken cancellationToken) =>
                originAuth.LogoutAsync(cancellationToken);

            public void SetVerificationListener(IWeb3VerifiedAuthenticator.VerificationDelegate? callback) =>
                originAuth.SetVerificationListener(callback);

            public void SetOtpRequestListener(IWeb3VerifiedAuthenticator.OtpRequestDelegate? callback) =>
                originAuth.SetOtpRequestListener(callback);
        }
    }
}
