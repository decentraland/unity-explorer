using Cysharp.Threading.Tasks;
using DCL.Web3;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using System;
using System.Threading;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    /// <summary>
    ///     Analytics decorator for ICompositeWeb3Provider.
    ///     Identifies user in analytics after successful login.
    /// </summary>
    public class AnalyticsDecoratorCompositeProvider : ICompositeWeb3Provider
    {
        private readonly ICompositeWeb3Provider core;
        private readonly IAnalyticsController analytics;

        public AnalyticsDecoratorCompositeProvider(ICompositeWeb3Provider core, IAnalyticsController analytics)
        {
            this.core = core ?? throw new ArgumentNullException(nameof(core));
            this.analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
        }

        // ICompositeWeb3Provider specific
        public AuthProvider CurrentProvider
        {
            get => core.CurrentProvider;
            set => core.CurrentProvider = value;
        }

        public event Action<AuthProvider>? OnMethodChanged
        {
            add => core.OnMethodChanged += value;
            remove => core.OnMethodChanged -= value;
        }

        public bool IsThirdWebOTP => core.IsThirdWebOTP;
        public bool IsDappWallet => core.IsDappWallet;

        public void SetTransactionConfirmationCallback(TransactionConfirmationDelegate? callback) =>
            core.SetTransactionConfirmationCallback(callback);

        // IDappVerificationHandler
        public event Action<(int code, DateTime expiration, string requestId)>? VerificationRequired
        {
            add => core.VerificationRequired += value;
            remove => core.VerificationRequired -= value;
        }

        public void CancelCurrentWeb3Operation() =>
            core.CancelCurrentWeb3Operation();

        // IOtpAuthenticator
        public UniTask SubmitOtp(string otp) =>
            core.SubmitOtp(otp);

        public UniTask ResendOtp() =>
            core.ResendOtp();

        public UniTask<bool> TryAutoLoginAsync(CancellationToken ct) =>
            core.TryAutoLoginAsync(ct);

        // IWeb3Authenticator
        public async UniTask<IWeb3Identity> LoginAsync(LoginPayload payload, CancellationToken ct)
        {
            IWeb3Identity identity = await core.LoginAsync(payload, ct);
            analytics.Identify(identity);
            return identity;
        }

        public UniTask LogoutAsync(CancellationToken ct) =>
            core.LogoutAsync(ct);

        // IEthereumApi
        public UniTask<EthApiResponse> SendAsync(EthApiRequest request, Web3RequestSource source, CancellationToken ct) =>
            core.SendAsync(request, source, ct);

        public void Dispose() =>
            core.Dispose();
    }
}
