using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Abstract;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using System;
using System.Numerics;
using System.Threading;
using Thirdweb;
using Thirdweb.Unity;
using UnityEngine;

namespace DCL.Web3.Authenticators
{
    /// <summary>
    ///     Thirdweb InApp Wallet authenticator. Uses email-based login and builds DCL AuthChain with an ephemeral account.
    /// </summary>
    public class ThirdwebInAppWalletAuthenticator : IWeb3VerifiedAuthenticator
    {
        private readonly IWeb3AccountFactory web3AccountFactory;
        private readonly DecentralandEnvironment environment;
        private readonly string loginEmail;

        public ThirdwebInAppWalletAuthenticator(IWeb3AccountFactory web3AccountFactory, DecentralandEnvironment environment, string loginEmail)
        {
            this.web3AccountFactory = web3AccountFactory;
            this.environment = environment;
            this.loginEmail = loginEmail;
        }

        public void Dispose() { }

        public async UniTask<IWeb3Identity> LoginAsync(CancellationToken ct)
        {
            await UniTask.SwitchToMainThread(ct);

            BigInteger chainId = GetChainId(environment);
            Debug.Log($"VVV ThirdwebAuth: Start login. Email={loginEmail}, Env={environment}, ChainId={chainId}");

            var walletOptions = new WalletOptions(
                WalletProvider.InAppWallet,
                chainId,
                new InAppWalletOptions(loginEmail)
            );

            Debug.Log("VVV ThirdwebAuth: ConnectWallet()");
            IThirdwebWallet wallet = await ThirdwebManager.Instance.ConnectWallet(walletOptions);
            Debug.Log("VVV ThirdwebAuth: ConnectWallet() completed");

            string sender = await wallet.GetAddress();
            Debug.Log($"VVV ThirdwebAuth: Wallet address={sender}");

            IWeb3Account ephemeralAccount = web3AccountFactory.CreateRandomAccount();
            DateTime sessionExpiration = DateTime.UtcNow.AddDays(7);

            string ephemeralMessage = CreateEphemeralMessage(ephemeralAccount, sessionExpiration);
            Debug.Log($"VVV ThirdwebAuth: Ephemeral generated. Addr={ephemeralAccount.Address}, Exp={sessionExpiration:s}");

            Debug.Log("VVV ThirdwebAuth: PersonalSign() start");
            string signature = await ThirdwebManager.Instance.ActiveWallet.PersonalSign(ephemeralMessage);
            Debug.Log("VVV ThirdwebAuth: PersonalSign() done");

            var authChain = AuthChain.Create();
            authChain.SetSigner(sender.ToLower());

            authChain.Set(new AuthLink
            {
                type = AuthLinkType.ECDSA_EPHEMERAL,
                payload = ephemeralMessage,
                signature = signature,
            });

            Debug.Log("VVV ThirdwebAuth: AuthChain built");

            return new DecentralandIdentity(new Web3Address(sender), ephemeralAccount, sessionExpiration, authChain);
        }

        public async UniTask LogoutAsync(CancellationToken cancellationToken)
        {
            await UniTask.SwitchToMainThread(cancellationToken);
            Debug.Log("VVV ThirdwebAuth: DisconnectWallet()");
            await ThirdwebManager.Instance.DisconnectWallet();
            Debug.Log("VVV ThirdwebAuth: Disconnected");
        }

        public void SetVerificationListener(IWeb3VerifiedAuthenticator.VerificationDelegate? callback) { }

        private static BigInteger GetChainId(DecentralandEnvironment environment) =>
            environment is DecentralandEnvironment.Org or DecentralandEnvironment.Today ? new BigInteger(1) : new BigInteger(11155111);

        private static string CreateEphemeralMessage(IWeb3Account ephemeralAccount, DateTime expiration) =>
            $"Decentraland Login\nEphemeral address: {ephemeralAccount.Address.OriginalFormat}\nExpiration: {expiration:yyyy-MM-ddTHH:mm:ss.fffZ}";
    }
}
