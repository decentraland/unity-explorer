using Cysharp.Threading.Tasks;
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

        public CompositeWeb3Provider(ThirdWebAuthenticator thirdWebAuth, DappWeb3Authenticator dappAuth)
        {
            this.thirdWebAuth = thirdWebAuth ?? throw new ArgumentNullException(nameof(thirdWebAuth));
            this.dappAuth = dappAuth ?? throw new ArgumentNullException(nameof(dappAuth));
        }

        // IWeb3Authenticator
        public UniTask<IWeb3Identity> LoginAsync(LoginPayload payload, CancellationToken ct) =>
            CurrentAuthenticator.LoginAsync(payload, ct);

        public UniTask LogoutAsync(CancellationToken ct) =>
            CurrentAuthenticator.LogoutAsync(ct);

        // IDappVerificationHandler - only dappAuth supports this
        public void CancelCurrentWeb3Operation() =>
            dappAuth.CancelCurrentWeb3Operation();

        // IOtpAuthenticator - only thirdWebAuth supports these
        public UniTask SubmitOtpAsync(string otp) =>
            thirdWebAuth.SubmitOtpAsync(otp);

        public UniTask ResendOtpAsync() =>
            thirdWebAuth.ResendOtpAsync();

        public UniTask<bool> TryAutoLoginAsync(CancellationToken ct)
        {
            // Temporary heuristic: if we have a stored email, assume ThirdWeb OTP flow; otherwise default to Dapp Wallet.
            string email = DCLPlayerPrefs.GetString(DCLPrefKeys.LOGGEDIN_EMAIL, string.Empty);
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
        }
    }
}
