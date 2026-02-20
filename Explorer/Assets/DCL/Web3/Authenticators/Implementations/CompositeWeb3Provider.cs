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
    ///     Implements ICompositeWeb3Provider which combines IWeb3Authenticator, IEthereumApi,
    ///     IDappVerificationHandler and IOtpAuthenticator to provide a single entry point for all Web3 needs.
    /// </summary>
    public class CompositeWeb3Provider : ICompositeWeb3Provider
    {
        private readonly ThirdWebAuthenticator thirdWebAuth;
        private readonly DappWeb3Authenticator dappAuth;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IAnalyticsController analytics;

        public AuthProvider CurrentProvider { get; set; } = AuthProvider.Dapp;

        // IDappVerificationHandler - delegates to dappAuth
        public event Action<(int code, DateTime expiration, string requestId)>? VerificationRequired
        {
            add => dappAuth.VerificationRequired += value;
            remove => dappAuth.VerificationRequired -= value;
        }

        public bool IsThirdWebOTP => CurrentProvider == AuthProvider.ThirdWeb;

        private IWeb3Authenticator currentAuthenticator => CurrentProvider == AuthProvider.ThirdWeb ? thirdWebAuth : dappAuth;
        private IEthereumApi currentEthereumApi => CurrentProvider == AuthProvider.ThirdWeb ? thirdWebAuth : dappAuth;

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

        public void Dispose()
        {
            thirdWebAuth.Dispose();
            dappAuth.Dispose();
            identityCache.Dispose();
        }

        // IWeb3Authenticator
        public async UniTask<IWeb3Identity> LoginAsync(LoginPayload payload, CancellationToken ct)
        {
            IWeb3Identity identity = await currentAuthenticator.LoginAsync(payload, ct);
            identityCache.Identity = identity;
            analytics.Identify(identity);
            return identity;
        }

        public async UniTask LogoutAsync(CancellationToken ct)
        {
            analytics.Identify(null);
            await currentAuthenticator.LogoutAsync(ct);
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
