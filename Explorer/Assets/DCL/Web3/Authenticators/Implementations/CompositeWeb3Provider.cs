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
    ///     Implements ICompositeWeb3Provider which combines IWeb3VerifiedAuthenticator and IEthereumApi
    ///     to ensure that switching auth method also switches the Web3 API provider for scenes.
    /// </summary>
    public class CompositeWeb3Provider : ICompositeWeb3Provider
    {
        private readonly ThirdWebAuthenticator thirdWebAuth;
        private readonly DappWeb3Authenticator dappAuth;

        private AuthMethod currentMethod = AuthMethod.DappWallet;

        public AuthMethod CurrentMethod
        {
            get => currentMethod;

            set
            {
                if (currentMethod != value)
                {
                    currentMethod = value;
                    OnMethodChanged?.Invoke(value);
                }
            }
        }

        public event Action<(int code, DateTime expiration, string requestId)>? VerificationRequired
        {
            add => dappAuth.VerificationRequired += value;
            remove => dappAuth.VerificationRequired -= value;
        }

        public event Action<AuthMethod>? OnMethodChanged;
        public bool IsThirdWebOTP => currentMethod == AuthMethod.ThirdWebOTP;
        public bool IsDappWallet => currentMethod == AuthMethod.DappWallet;

        private IWeb3VerifiedAuthenticator CurrentAuthenticator => currentMethod == AuthMethod.ThirdWebOTP ? thirdWebAuth : dappAuth;

        private IEthereumApi CurrentEthereumApi => currentMethod == AuthMethod.ThirdWebOTP ? thirdWebAuth : dappAuth;

        public CompositeWeb3Provider(ThirdWebAuthenticator thirdWebAuth, DappWeb3Authenticator dappAuth)
        {
            this.thirdWebAuth = thirdWebAuth ?? throw new ArgumentNullException(nameof(thirdWebAuth));
            this.dappAuth = dappAuth ?? throw new ArgumentNullException(nameof(dappAuth));
        }

        public UniTask<IWeb3Identity> LoginAsync(LoginMethod loginMethod, CancellationToken ct) =>
            CurrentAuthenticator.LoginAsync(loginMethod, ct);

        public UniTask<IWeb3Identity> LoginPayloadedAsync<TPayload>(LoginMethod method, TPayload payload, CancellationToken ct) =>
            CurrentAuthenticator.LoginPayloadedAsync(method, payload, ct);

        public UniTask LogoutAsync(CancellationToken ct) =>
            CurrentAuthenticator.LogoutAsync(ct);

        public void CancelCurrentWeb3Operation()
        {
            thirdWebAuth.CancelCurrentWeb3Operation();
            dappAuth.CancelCurrentWeb3Operation();
        }

        public UniTask SubmitOtp(string otp) =>
            thirdWebAuth.SubmitOtp(otp);

        public UniTask ResendOtp() =>
            thirdWebAuth.ResendOtp();

        public UniTask<bool> TryAutoConnectAsync(CancellationToken ct)
        {
            // Temporary heuristic: if we have a stored email, assume ThirdWeb OTP flow; otherwise default to Dapp Wallet.
            string email = DCLPlayerPrefs.GetString(DCLPrefKeys.LOGGEDIN_EMAIL, string.Empty);
            CurrentMethod = string.IsNullOrEmpty(email) ? AuthMethod.DappWallet : AuthMethod.ThirdWebOTP;

            return CurrentAuthenticator.TryAutoConnectAsync(ct);
        }

        public UniTask<EthApiResponse> SendAsync(EthApiRequest request, CancellationToken ct) =>
            CurrentEthereumApi.SendAsync(request, ct);

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
