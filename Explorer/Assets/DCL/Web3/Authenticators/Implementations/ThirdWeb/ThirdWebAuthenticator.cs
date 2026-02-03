using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Abstract;
using DCL.Web3.Identities;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using Thirdweb;
using UnityEngine;

namespace DCL.Web3.Authenticators
{
    public class ThirdWebAuthenticator : IWeb3Authenticator, IEthereumApi, IOtpAuthenticator
    {
        private const string CLIENT_ID = "e1adce863fe287bb6cf0e3fd90bdb77f";
        private const string BUNDLE_ID = "com.Decentraland";
        private const string SDK_VERSION = "6.0.5";

        /// <summary>
        ///     RPC overrides for different chains. Uses Decentraland RPC endpoints.
        /// </summary>
        private static readonly Dictionary<BigInteger, string> RPC_OVERRIDES = new ()
        {
            { 1, "https://rpc.decentraland.org/mainnet" }, // Ethereum Mainnet
            { 11155111, "https://rpc.decentraland.org/sepolia" }, // Ethereum Sepolia
            { 137, "https://rpc.decentraland.org/polygon" }, // Polygon Mainnet
            { 80002, "https://rpc.decentraland.org/amoy" }, // Polygon Amoy
            { 42161, "https://rpc.decentraland.org/arbitrum" }, // Arbitrum Mainnet
            { 10, "https://rpc.decentraland.org/optimism" }, // Optimism Mainnet
            { 43114, "https://rpc.decentraland.org/avalanche" }, // Avalanche Mainnet
            { 56, "https://rpc.decentraland.org/binance" }, // BSC Mainnet
            { 250, "https://rpc.decentraland.org/fantom" }, // Fantom Mainnet
        };

        private readonly ThirdWebLoginService loginService;
        private readonly ThirdWebEthereumApi ethereumApi;

        private IThirdwebWallet? activeWallet => loginService.ActiveWallet;

        internal ThirdWebAuthenticator(
            IDecentralandUrlsSource decentralandUrlsSource,
            DecentralandEnvironment environment,
            HashSet<string> whitelistMethods,
            HashSet<string> readOnlyMethods,
            IWeb3AccountFactory web3AccountFactory,
            int? identityExpirationDuration = null)
        {
            var thirdwebClient = ThirdwebClient.Create(
                CLIENT_ID,
                bundleId: BUNDLE_ID,
                httpClient: new ThirdwebHttpClient(),
                sdkName: "UnitySDK",
                sdkOs: Application.platform.ToString(),
                sdkPlatform: "unity",
                sdkVersion: SDK_VERSION,
                rpcOverrides: RPC_OVERRIDES
            );

            loginService = new ThirdWebLoginService(thirdwebClient, web3AccountFactory, identityExpirationDuration);
            ethereumApi = new ThirdWebEthereumApi(thirdwebClient, whitelistMethods, readOnlyMethods, decentralandUrlsSource, environment);
        }

        public void Dispose()
        {
            // Logout on Dispose will close ThirdWeb session and break ThirdWeb auto-login.
            // So we need to keep session open for auto-login to work.
        }

        // Authenticator API
        public async UniTask<bool> TryAutoLoginAsync(CancellationToken ct) =>
            await loginService.TryAutoLoginAsync(ct);

        public async UniTask<IWeb3Identity> LoginAsync(LoginPayload payload, CancellationToken ct) =>
            await loginService.LoginAsync(payload, ct);

        public async UniTask LogoutAsync(CancellationToken ct) =>
            await loginService.LogoutAsync(ct);

        public async UniTask SubmitOtpAsync(string otp, CancellationToken ct = default) =>
            await loginService.SubmitOtpAsync(otp, ct);

        public async UniTask ResendOtpAsync(CancellationToken ct = default) =>
            await loginService.ResendOtpAsync(ct);

        // Ethereum API
        public UniTask<EthApiResponse> SendAsync(EthApiRequest request, Web3RequestSource source, CancellationToken ct) =>
            ethereumApi.SendAsync(activeWallet, request, source, ct);

        public void SetTransactionConfirmationCallback(TransactionConfirmationDelegate? callback) =>
            ethereumApi.TransactionConfirmationCallback = callback;
    }
}
