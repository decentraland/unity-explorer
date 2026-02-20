using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
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
    ///     Implements ICompositeWeb3Provider which combines IWeb3Authenticator, IEthereumApi,
    ///     IDappVerificationHandler and IOtpAuthenticator to provide a single entry point for all Web3 needs.
    /// </summary>
    public class CompositeWeb3Provider : ICompositeWeb3Provider
    {
        private readonly ThirdWebAuthenticator thirdWebAuth;
        private readonly DappWeb3Authenticator dappAuth;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IAnalyticsController analytics;

        private AuthProvider currentProvider = AuthProvider.Dapp;

        public AuthProvider CurrentProvider
        {
            get => currentProvider;

            set
            {
                if (currentProvider != value)
                {
                    currentProvider = value;
                    OnMethodChanged?.Invoke(value);
                }
            }
        }

        // IDappVerificationHandler - delegates to dappAuth
        public event Action<(int code, DateTime expiration, string requestId)>? VerificationRequired
        {
            add => dappAuth.VerificationRequired += value;
            remove => dappAuth.VerificationRequired -= value;
        }

        public event Action<AuthProvider>? OnMethodChanged;
        public bool IsThirdWebOTP => currentProvider == AuthProvider.ThirdWeb;
        public bool IsDappWallet => currentProvider == AuthProvider.Dapp;

        private IWeb3Authenticator CurrentAuthenticator => currentProvider == AuthProvider.ThirdWeb ? thirdWebAuth : dappAuth;

        private IEthereumApi CurrentEthereumApi => currentProvider == AuthProvider.ThirdWeb ? thirdWebAuth : dappAuth;

        public CompositeWeb3Provider(
            ThirdWebAuthenticator thirdWebAuth,
            DappWeb3Authenticator dappAuth,
            IWeb3IdentityCache identityCache,
            IAnalyticsController analytics)
        {
            this.thirdWebAuth = thirdWebAuth ?? throw new ArgumentNullException(nameof(thirdWebAuth));
            this.dappAuth = dappAuth ?? throw new ArgumentNullException(nameof(dappAuth));
            this.identityCache = identityCache ?? throw new ArgumentNullException(nameof(identityCache));
            this.analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
        }

        // IWeb3Authenticator
        public async UniTask<IWeb3Identity> LoginAsync(LoginPayload payload, CancellationToken ct)
        {
            IWeb3Identity identity = await CurrentAuthenticator.LoginAsync(payload, ct);
            identityCache.Identity = identity;
            analytics.Identify(identity);
            return identity;
        }

        public async UniTask LogoutAsync(CancellationToken ct)
        {
            analytics.Identify(null);
            await CurrentAuthenticator.LogoutAsync(ct);
            identityCache.Clear();
        }

        // IDappVerificationHandler - only dappAuth supports this
        public void CancelCurrentWeb3Operation() =>
            dappAuth.CancelCurrentWeb3Operation();

        // IOtpAuthenticator - only thirdWebAuth supports these
        public UniTask SubmitOtpAsync(string otp, CancellationToken ct = default) =>
            thirdWebAuth.SubmitOtpAsync(otp, ct);

        public UniTask ResendOtpAsync(CancellationToken ct = default) =>
            thirdWebAuth.ResendOtpAsync(ct);

        public UniTask<bool> TryAutoLoginAsync(CancellationToken ct)
        {
            string email = DCLPlayerPrefs.GetString(DCLPrefKeys.LOGGEDIN_EMAIL, string.Empty);
            bool emailOtpEnabled = FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.EMAIL_OTP_AUTH);

            // If OTP feature flag is disabled, skip ThirdWeb auto-login entirely — even if there is a stored email.
            // This prevents getting stuck at the splash screen when the FF is turned off while the user has a previous OTP session.
            if (!emailOtpEnabled && !string.IsNullOrEmpty(email))
            {
                ReportHub.Log(ReportCategory.AUTHENTICATION, "OTP email feature flag is disabled — clearing stored email and skipping ThirdWeb auto-login");
                DCLPlayerPrefs.DeleteKey(DCLPrefKeys.LOGGEDIN_EMAIL, save: true);
                CurrentProvider = AuthProvider.Dapp;
                return UniTask.FromResult(true);
            }

            // Heuristic: if we have a stored email, assume ThirdWeb OTP flow; otherwise default to Dapp Wallet.
            CurrentProvider = string.IsNullOrEmpty(email) ? AuthProvider.Dapp : AuthProvider.ThirdWeb;

            // Only ThirdWeb supports auto-login
            return CurrentProvider == AuthProvider.ThirdWeb
                ? thirdWebAuth.TryAutoLoginAsync(ct)
                : UniTask.FromResult(true);
        }

        // IEthereumApi
        public UniTask<EthApiResponse> SendAsync(EthApiRequest request, Web3RequestSource source, CancellationToken ct) =>
            CurrentEthereumApi.SendAsync(request, source, ct);

        public void SetTransactionConfirmationCallback(TransactionConfirmationDelegate? callback)
        {
            thirdWebAuth.SetTransactionConfirmationCallback(callback);
        }

        public void Dispose()
        {
            thirdWebAuth.Dispose();
            dappAuth.Dispose();
            identityCache.Dispose();
        }
    }
}
