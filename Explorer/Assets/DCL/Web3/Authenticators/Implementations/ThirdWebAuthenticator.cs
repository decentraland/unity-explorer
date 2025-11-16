using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Abstract;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using System;
using System.Collections.Generic;
using System.Threading;
using Thirdweb;
using ThirdWebUnity;
using ThirdWebUnity.Playground;

namespace DCL.Web3.Authenticators
{
    public class ThirdWebAuthenticator : IWeb3VerifiedAuthenticator, IVerifiedEthereumApi
    {
        private readonly SemaphoreSlim mutex = new (1, 1);

        private readonly HashSet<string> whitelistMethods;
        private readonly IWeb3IdentityCache identityCache;
        private readonly DecentralandEnvironment environment;
        private readonly IWeb3AccountFactory web3AccountFactory;
        private readonly int? identityExpirationDuration;

        public ThirdWebAuthenticator(DecentralandEnvironment environment, IWeb3IdentityCache identityCache, HashSet<string> whitelistMethods,
            IWeb3AccountFactory web3AccountFactory, int? identityExpirationDuration = null)
        {
            this.environment = environment;
            this.identityCache = identityCache;
            this.whitelistMethods = whitelistMethods;
            this.web3AccountFactory = web3AccountFactory;
            this.identityExpirationDuration = identityExpirationDuration;
        }

        public async UniTask<IWeb3Identity> LoginAsync(string email, string password, CancellationToken ct)
        {
            await mutex.WaitAsync(ct);

            SynchronizationContext originalSyncContext = SynchronizationContext.Current;

            try
            {
                await UniTask.SwitchToMainThread(ct);

                string? jwt = await ThirdWebCustomJWTAuth.GetJWT(email, password);

                var walletOptions = new ThirdWebManager.WalletOptions(
                    ThirdWebManager.WalletProvider.InAppWallet,
                    EnvChainsUtils.GetChainIdAsInt(environment),
                    new ThirdWebManager.InAppWalletOptions(authprovider: AuthProvider.JWT, jwtOrPayload: jwt)
                );

                IThirdwebWallet wallet = await ThirdWebManager.Instance.ConnectWallet(walletOptions);
                string sender = await wallet.GetAddress();

                IWeb3Account ephemeralAccount = web3AccountFactory.CreateRandomAccount();

                // 1 week expiration day, just like unity-renderer
                DateTime sessionExpiration = identityExpirationDuration != null
                    ? DateTime.UtcNow.AddSeconds(identityExpirationDuration.Value)
                    : DateTime.UtcNow.AddDays(7);

                var ephemeralMessage =
                    $"Decentraland Login\nEphemeral address: {ephemeralAccount.Address.OriginalFormat}\nExpiration: {sessionExpiration:yyyy-MM-ddTHH:mm:ss.fffZ}";

                string signature = await ThirdWebManager.Instance.ActiveWallet.PersonalSign(ephemeralMessage);

                var authChain = AuthChain.Create();
                authChain.SetSigner(sender.ToLower());

                authChain.Set(new AuthLink
                {
                    type = signature.Length == 132
                        ? AuthLinkType.ECDSA_EPHEMERAL
                        : AuthLinkType.ECDSA_EIP_1654_EPHEMERAL,
                    payload = ephemeralMessage,
                    signature = signature,
                });

                return new DecentralandIdentity(new Web3Address(sender), ephemeralAccount, sessionExpiration, authChain);
            }
            catch (Exception)
            {
                await LogoutAsync(CancellationToken.None);
                throw;
            }
            finally
            {
                if (originalSyncContext != null)
                    await UniTask.SwitchToSynchronizationContext(originalSyncContext, CancellationToken.None);
                else
                    await UniTask.SwitchToMainThread(CancellationToken.None);

                mutex.Release();
            }
        }

        public void Dispose() =>
            LogoutAsync(CancellationToken.None).Forget();

        public async UniTask LogoutAsync(CancellationToken cancellationToken) =>
            await ThirdWebManager.Instance.DisconnectWallet();

        public async UniTask<EthApiResponse> SendAsync(EthApiRequest request, CancellationToken ct)
        {
            if (!whitelistMethods.Contains(request.method))
                throw new Web3Exception($"The method is not allowed: {request.method}");

            if (string.Equals(request.method, "eth_accounts")
                || string.Equals(request.method, "eth_requestAccounts"))
            {
                string[] accounts = Array.Empty<string>();

                if (identityCache.Identity != null)
                    accounts = new string[] { identityCache.EnsuredIdentity().Address };

                return new EthApiResponse
                {
                    id = request.id,
                    jsonrpc = "2.0",
                    result = accounts,
                };
            }

            if (string.Equals(request.method, "eth_chainId"))
            {
                string chainId = EnvChainsUtils.GetChainId(environment);

                return new EthApiResponse
                {
                    id = request.id,
                    jsonrpc = "2.0",
                    result = chainId,
                };
            }

            if (string.Equals(request.method, "net_version"))
            {
                string netVersion = EnvChainsUtils.GetNetVersion(environment);

                return new EthApiResponse
                {
                    id = request.id,
                    jsonrpc = "2.0",
                    result = netVersion,
                };
            }

            throw new NotImplementedException();

            // if (IsReadOnly(request))
            //     return await SendWithoutConfirmationAsync(request, ct);
            //
            // return await SendWithConfirmationAsync(request, ct);
        }

        public void SetVerificationListener(IWeb3VerifiedAuthenticator.VerificationDelegate? callback)
        {
        }

        public void AddVerificationListener(IVerifiedEthereumApi.VerificationDelegate callback)
        {
        }
    }
}
