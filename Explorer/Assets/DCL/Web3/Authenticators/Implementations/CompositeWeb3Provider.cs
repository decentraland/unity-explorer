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
        private readonly EphemeralWeb3Authenticator ephemeralGuestLogin;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IAnalyticsController analytics;

        public AuthProvider CurrentProvider { private get; set; } = AuthProvider.Dapp;

        public event Action<string>? OTPSendSucceeded
        {
            add => thirdWebAuth.OTPSendSucceeded += value;
            remove => thirdWebAuth.OTPSendSucceeded -= value;
        }

        private IEthereumApi currentEthereumApi => identityCache.IsThirdWebAccount() ? thirdWebAuth : dappEthereumApi;

        public CompositeWeb3Provider(
            ThirdWebAuthenticator thirdWebAuth,
            DappWeb3EthereumApi dappEthereumApi,
            DappDeepLinkAuthenticator dappLogin,
            IWeb3IdentityCache identityCache,
            IAnalyticsController analytics,
            EphemeralWeb3Authenticator ephemeralGuestLogin)
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

            IWeb3Authenticator currentAuthenticator = CurrentProvider switch
                                                      {
                                                          AuthProvider.ThirdWeb => thirdWebAuth,
                                                          AuthProvider.Ephemeral => ephemeralGuestLogin,
                                                          _ => dappLogin,
                                                      };

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

            switch (identityCache.Identity?.Method)
            {
                case LoginMethod.EMAIL_OTP:
                case LoginMethod.GUEST:
                    await thirdWebAuth.LogoutAsync(ct);
                    break;

                // The account only ever lived in the identity that is cleared below
                case LoginMethod.EPHEMERAL_GUEST: break;

                default:
                    await dappEthereumApi.DisconnectFromAuthApiAsync();
                    break;
            }

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

            if (!GuestLoginIsEnabled())
                DCLPlayerPrefs.DeleteKey(DCLPrefKeys.GUEST_SESSION_ACTIVE, save: true);

            // Auto-login only works for thirdweb accounts
            if (!identityCache.IsThirdWebAccount())
                throw new AutoLoginNotNeededException();

            // Only the ThirdWeb guest flow stores this flag, so it is the one that has a session to restore
            if (DCLPlayerPrefs.GetBool(DCLPrefKeys.GUEST_SESSION_ACTIVE))
            {
                try { return await thirdWebAuth.TryAutoLoginAsync(ct); }
                catch (GuestAccountUpgradedException)
                {
                    DiscardUpgradedGuestSession();
                    return false;
                }
            }

            string storedEmail = DCLPlayerPrefs.GetString(DCLPrefKeys.LOGGEDIN_EMAIL, string.Empty);

            if (!string.IsNullOrEmpty(storedEmail))
                return await thirdWebAuth.TryAutoLoginAsync(ct);

            return true;

            bool OtpIsDisabled() =>
                !FeaturesRegistry.Instance.IsEnabled(FeatureId.EmailOTPAuth);

            bool GuestLoginIsEnabled() =>
                FeaturesRegistry.Instance.IsEnabled(FeatureId.GuestLogin);
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
