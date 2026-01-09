using Cysharp.Threading.Tasks;
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

        /// <summary>
        ///     Event fired when the authentication method changes
        /// </summary>
        public event Action<AuthMethod>? OnMethodChanged;

        /// <summary>
        ///     Returns true if ThirdWeb OTP method is currently selected
        /// </summary>
        public bool IsThirdWebOTP => currentMethod == AuthMethod.ThirdWebOTP;

        /// <summary>
        ///     Returns true if Dapp Wallet method is currently selected
        /// </summary>
        public bool IsDappWallet => currentMethod == AuthMethod.DappWallet;

        private IWeb3VerifiedAuthenticator CurrentAuthenticator => currentMethod == AuthMethod.ThirdWebOTP ? thirdWebAuth : dappAuth;

        private IEthereumApi CurrentEthereumApi => currentMethod == AuthMethod.ThirdWebOTP ? thirdWebAuth : dappAuth;

        public CompositeWeb3Provider(ThirdWebAuthenticator thirdWebAuth, DappWeb3Authenticator dappAuth)
        {
            this.thirdWebAuth = thirdWebAuth ?? throw new ArgumentNullException(nameof(thirdWebAuth));
            this.dappAuth = dappAuth ?? throw new ArgumentNullException(nameof(dappAuth));
        }

        public UniTask<IWeb3Identity> LoginAsync(string email, CancellationToken ct) =>
            CurrentAuthenticator.LoginAsync(email, ct);

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

        public UniTask<bool> TryAutoConnectAsync(CancellationToken ct) =>
            thirdWebAuth.TryAutoConnectAsync(ct);

        public UniTask<EthApiResponse> SendAsync(EthApiRequest request, CancellationToken ct) =>
            CurrentEthereumApi.SendAsync(request, ct);

        public void Dispose()
        {
            thirdWebAuth.Dispose();
            dappAuth.Dispose();
        }
    }
}
