using Cysharp.Threading.Tasks;
using DCL.FeatureFlags;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Prefs;
using DCL.Web3.Identities;
using System;
using System.Threading;

namespace DCL.Web3.Authenticators
{
    /// <summary>
    ///     Composite provider that wraps both authentication methods (ThirdWeb OTP and Dapp Wallet)
    ///     and delegates calls to the currently selected method.
    ///     Implements ICompositeWeb3Provider which combines IWeb3Authenticator, IEthereumApi
    ///     and IOtpAuthenticator to provide a single entry point for all Web3 needs.
    /// </summary>
    public class CompositeWeb3Provider : ICompositeWeb3Provider
    {
        private readonly ThirdWebAuthenticator thirdWebAuth;
        private readonly DappWeb3EthereumApi dappEthereumApi;
        private readonly IWeb3Authenticator dappLogin;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IAnalyticsController analytics;

        public AuthProvider CurrentProvider { private get; set; } = AuthProvider.Dapp;

        public event Action<string>? OTPSendSucceeded
        {
            add => thirdWebAuth.OTPSendSucceeded += value;
            remove => thirdWebAuth.OTPSendSucceeded -= value;
        }

        public bool IsThirdWebOTP => CurrentProvider == AuthProvider.ThirdWeb;

        private IWeb3Authenticator currentAuthenticator => CurrentProvider == AuthProvider.ThirdWeb ? thirdWebAuth : dappLogin;
        private IEthereumApi currentEthereumApi => CurrentProvider == AuthProvider.ThirdWeb ? thirdWebAuth : dappEthereumApi;

        public CompositeWeb3Provider(
            ThirdWebAuthenticator thirdWebAuth,
            DappWeb3EthereumApi dappEthereumApi,
            DappDeepLinkAuthenticator dappLogin,
            IWeb3IdentityCache identityCache,
            IAnalyticsController analytics)
        {
            this.thirdWebAuth = thirdWebAuth ?? throw new ArgumentNullException(nameof(thirdWebAuth));
            this.dappEthereumApi = dappEthereumApi ?? throw new ArgumentNullException(nameof(dappEthereumApi));
            this.dappLogin = dappLogin ?? throw new ArgumentNullException(nameof(dappLogin));
            this.identityCache = identityCache ?? throw new ArgumentNullException(nameof(identityCache));
            this.analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
        }

        public void Dispose()
        {
            thirdWebAuth.Dispose();
            dappEthereumApi.Dispose();
            dappLogin.Dispose();
            identityCache.Dispose();
        }

        // IWeb3Authenticator
        public async UniTask<IWeb3Identity> LoginAsync(LoginPayload payload, CancellationToken ct)
        {
            IWeb3Identity identity = await currentAuthenticator.LoginAsync(payload, ct);
            identityCache.Identity = identity;
            analytics.Identify(identity);

            if (identity.Source != IWeb3Identity.Web3IdentitySource.OTP)
                DCLPlayerPrefs.DeleteKey(DCLPrefKeys.LOGGEDIN_EMAIL, save: true);

            return identity;
        }

        public async UniTask LogoutAsync(CancellationToken ct)
        {
            analytics.Identify(null);

            // ThirdWeb is the only provider holding a login session of its own.
            if (IsThirdWebOTP)
                await thirdWebAuth.LogoutAsync(ct);
            else
                // Abort any in-flight browser signature confirmation so an approval arriving
                // after logout cannot complete under the logged-out session.
                await dappEthereumApi.DisconnectFromAuthApiAsync();

            identityCache.Clear();
        }

        // IOtpAuthenticator - only thirdWebAuth supports these
        public UniTask SubmitOtpAsync(string otp, CancellationToken ct = default) =>
            thirdWebAuth.SubmitOtpAsync(otp, ct);

        public UniTask ResendOtpAsync(CancellationToken ct = default) =>
            thirdWebAuth.ResendOtpAsync(ct);

        public UniTask<bool> TryAutoLoginAsync(CancellationToken ct)
        {
            if (OtpIsDisabled())
                DCLPlayerPrefs.DeleteKey(DCLPrefKeys.LOGGEDIN_EMAIL, save: true);

            string storedEmail = DCLPlayerPrefs.GetString(DCLPrefKeys.LOGGEDIN_EMAIL, string.Empty);

            // Heuristic: if we have a stored email, assume ThirdWeb OTP flow; otherwise default to Dapp Wallet.
            if (string.IsNullOrEmpty(storedEmail))
            {
                CurrentProvider = AuthProvider.Dapp;
                return UniTask.FromResult(true);
            }
            else
            {
                CurrentProvider = AuthProvider.ThirdWeb;
                return thirdWebAuth.TryAutoLoginAsync(ct);
            }

            bool OtpIsDisabled() => !FeaturesRegistry.Instance.IsEnabled(FeatureId.EMAIL_OTP_AUTH);
        }

        // IEthereumApi
        public UniTask<EthApiResponse> SendAsync(EthApiRequest request, Web3RequestSource source, CancellationToken ct) =>
            currentEthereumApi.SendAsync(request, source, ct);

        public void SetTransactionConfirmationCallback(TransactionConfirmationDelegate? callback) =>
            thirdWebAuth.SetTransactionConfirmationCallback(callback);
    }
}
