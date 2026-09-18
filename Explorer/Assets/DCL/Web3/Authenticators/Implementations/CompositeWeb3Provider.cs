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
        private readonly IWeb3Authenticator ephemeralGuestLogin;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IAnalyticsController analytics;

        public AuthProvider CurrentProvider { private get; set; } = AuthProvider.Dapp;

        public event Action<string>? OTPSendSucceeded
        {
            add => thirdWebAuth.OTPSendSucceeded += value;
            remove => thirdWebAuth.OTPSendSucceeded -= value;
        }

        public bool IsThirdWebAccount => CurrentProvider == AuthProvider.ThirdWeb;

        private IWeb3Authenticator currentAuthenticator => CurrentProvider switch
                                                           {
                                                               AuthProvider.ThirdWeb => thirdWebAuth,
                                                               AuthProvider.Ephemeral => ephemeralGuestLogin,
                                                               _ => dappLogin,
                                                           };
        private IEthereumApi currentEthereumApi => IsThirdWebAccount ? thirdWebAuth : dappEthereumApi;

        public CompositeWeb3Provider(
            ThirdWebAuthenticator thirdWebAuth,
            DappWeb3EthereumApi dappEthereumApi,
            DappDeepLinkAuthenticator dappLogin,
            IWeb3IdentityCache identityCache,
            IAnalyticsController analytics,
            IWeb3Authenticator ephemeralGuestLogin)
        {
            this.thirdWebAuth = thirdWebAuth ?? throw new ArgumentNullException(nameof(thirdWebAuth));
            this.dappEthereumApi = dappEthereumApi ?? throw new ArgumentNullException(nameof(dappEthereumApi));
            this.dappLogin = dappLogin ?? throw new ArgumentNullException(nameof(dappLogin));
            this.ephemeralGuestLogin = ephemeralGuestLogin ?? throw new ArgumentNullException(nameof(ephemeralGuestLogin));
            this.identityCache = identityCache ?? throw new ArgumentNullException(nameof(identityCache));
            this.analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
        }

        public void Dispose()
        {
            thirdWebAuth.Dispose();
            dappEthereumApi.Dispose();
            dappLogin.Dispose();
            ephemeralGuestLogin.Dispose();
            identityCache.Dispose();
        }

        // IWeb3Authenticator
        public async UniTask<IWeb3Identity> LoginAsync(LoginPayload payload, CancellationToken ct)
        {
            IWeb3Identity identity;

            try { identity = await currentAuthenticator.LoginAsync(payload, ct); }
            catch (GuestAccountUpgradedException)
            {
                DiscardUpgradedGuestSession();
                throw;
            }

            identityCache.Identity = identity;
            analytics.Identify(identity);

            if (identity.Method != LoginMethod.EMAIL_OTP)
                DCLPlayerPrefs.DeleteKey(DCLPrefKeys.LOGGEDIN_EMAIL, save: true);

            if (identity.Method != LoginMethod.GUEST)
                DCLPlayerPrefs.DeleteKey(DCLPrefKeys.GUEST_SESSION_ACTIVE, save: true);

            return identity;
        }

        public async UniTask LogoutAsync(CancellationToken ct)
        {
            analytics.Identify(null);

            // ThirdWeb is the only provider holding a login session of its own.
            if (IsThirdWebAccount)
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

        public UniTask SendEmailLinkOtpAsync(string email, CancellationToken ct) =>
            thirdWebAuth.SendEmailLinkOtpAsync(email, ct);

        public UniTask ResendEmailLinkOtpAsync(CancellationToken ct) =>
            thirdWebAuth.ResendEmailLinkOtpAsync(ct);

        public async UniTask<IWeb3Identity> LinkEmailAsync(string otp, CancellationToken ct)
        {
            IWeb3Identity identity = await thirdWebAuth.LinkEmailAsync(otp, ct);

            CurrentProvider = AuthProvider.ThirdWeb;
            identityCache.Identity = identity;
            analytics.Identify(identity);

            return identity;
        }

        public async UniTask<bool> TryAutoLoginAsync(CancellationToken ct)
        {
            if (OtpIsDisabled())
                DCLPlayerPrefs.DeleteKey(DCLPrefKeys.LOGGEDIN_EMAIL, save: true);

            if (GuestLoginIsDisabled())
                DCLPlayerPrefs.DeleteKey(DCLPrefKeys.GUEST_SESSION_ACTIVE, save: true);

            string storedEmail = DCLPlayerPrefs.GetString(DCLPrefKeys.LOGGEDIN_EMAIL, string.Empty);

            // Heuristic: a stored email means the ThirdWeb OTP flow; otherwise the stored identity tells whether
            // it was generated on the device as a guest, and anything else is a Dapp Wallet.
            if (string.IsNullOrEmpty(storedEmail))
            {
                CurrentProvider = identityCache.IsGuest() ? AuthProvider.Ephemeral : AuthProvider.Dapp;
                return true;
            }

            CurrentProvider = AuthProvider.ThirdWeb;
            return await thirdWebAuth.TryAutoLoginAsync(ct);

            bool OtpIsDisabled() => !FeaturesRegistry.Instance.IsEnabled(FeatureId.EmailOTPAuth);

            bool GuestLoginIsDisabled() => !FeaturesRegistry.Instance.IsEnabled(FeatureId.GuestLogin);
        }

        public UniTask<EthApiResponse> SendAsync(EthApiRequest request, Web3RequestSource source, CancellationToken ct) =>
            currentEthereumApi.SendAsync(request, source, ct);

        public void SetTransactionConfirmationCallback(TransactionConfirmationDelegate? callback) =>
            thirdWebAuth.SetTransactionConfirmationCallback(callback);

        private void DiscardUpgradedGuestSession()
        {
            identityCache.Clear();
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.GUEST_SESSION_ACTIVE, save: true);
        }
    }
}
