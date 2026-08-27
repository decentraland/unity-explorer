using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Abstract;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using DCL.WebRequests;
using System;
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

        private readonly ThirdWebLoginService loginService;
        private readonly ThirdWebEthereumApi ethereumApi;

        public event Action<string>? OTPSendSucceeded
        {
            add => loginService.OTPSendSucceeded += value;
            remove => loginService.OTPSendSucceeded -= value;
        }

        private IThirdwebWallet? activeWallet => loginService.ActiveWallet;

        internal ThirdWebAuthenticator(
            IDecentralandUrlsSource decentralandUrlsSource,
            EthereumNetwork ethereumNetwork,
            HashSet<string> whitelistMethods,
            HashSet<string> readOnlyMethods,
            IWeb3AccountFactory web3AccountFactory,
            IWebRequestController webRequestController,
            int? identityExpirationDuration = null,
            string? guestSessionIdOverride = null)
        {
            Dictionary<BigInteger, string> rpcOverrides = ChainRpcOverrides(decentralandUrlsSource);

            var thirdwebClient = ThirdwebClient.Create(
                CLIENT_ID,
                bundleId: BUNDLE_ID,
                httpClient: new DclThirdwebHttpClient(webRequestController),
                sdkName: "UnitySDK",
                sdkOs: Application.platform.ToString(),
                sdkPlatform: "unity",
                sdkVersion: SDK_VERSION,
                rpcOverrides: rpcOverrides
            );

            loginService = new ThirdWebLoginService(thirdwebClient, web3AccountFactory, identityExpirationDuration, guestSessionIdOverride);
            ethereumApi = new ThirdWebEthereumApi(thirdwebClient, whitelistMethods, readOnlyMethods, decentralandUrlsSource, ethereumNetwork, rpcOverrides);
        }

        /// <summary>
        ///     Where the embedded wallet reaches each chain it may transact on, keyed by chain id. These are
        ///     Decentraland's own RPC proxy rather than a chain's public node, so they follow the base domain like
        ///     every other backend host: a <c>--base-domain</c> deployment serves its chains from
        ///     <c>rpc.&lt;its domain&gt;</c>.
        ///     <para>
        ///         Probed rather than resolved through <c>Url</c> so the endpoint stays the RPC host itself. It is a
        ///         single-label subdomain, which would otherwise be rewritten to the gateway once that flag is on -
        ///         a change of route for every chain call, on the default environments too.
        ///     </para>
        /// </summary>
        private static Dictionary<BigInteger, string> ChainRpcOverrides(IDecentralandUrlsSource decentralandUrlsSource)
        {
            string chainRpc = decentralandUrlsSource.Probe(DecentralandUrl.ChainRpc);

            return new Dictionary<BigInteger, string>
            {
                { 1, $"{chainRpc}/mainnet" }, // Ethereum Mainnet
                { 11155111, $"{chainRpc}/sepolia" }, // Ethereum Sepolia
                { 137, $"{chainRpc}/polygon" }, // Polygon Mainnet
                { 80002, $"{chainRpc}/amoy" }, // Polygon Amoy
                { 42161, $"{chainRpc}/arbitrum" }, // Arbitrum Mainnet
                { 10, $"{chainRpc}/optimism" }, // Optimism Mainnet
                { 43114, $"{chainRpc}/avalanche" }, // Avalanche Mainnet
                { 56, $"{chainRpc}/binance" }, // BSC Mainnet
                { 250, $"{chainRpc}/fantom" }, // Fantom Mainnet
            };
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
