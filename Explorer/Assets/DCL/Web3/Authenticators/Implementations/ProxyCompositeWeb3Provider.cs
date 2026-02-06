using Cysharp.Threading.Tasks;
using DCL.Web3.Identities;
using System;
using System.Threading;

namespace DCL.Web3.Authenticators
{
    /// <summary>
    ///     Proxy wrapper for ICompositeWeb3Provider that caches identity after login.
    ///     This ensures the identity is stored in the cache after successful authentication.
    /// </summary>
    public class ProxyCompositeWeb3Provider : ICompositeWeb3Provider
    {
        private readonly ICompositeWeb3Provider provider;
        private readonly IWeb3IdentityCache identityCache;

        public ProxyCompositeWeb3Provider(
            ICompositeWeb3Provider provider,
            IWeb3IdentityCache identityCache)
        {
            this.provider = provider;
            this.identityCache = identityCache;
        }

        // ICompositeWeb3Provider specific
        public AuthProvider CurrentProvider
        {
            get => provider.CurrentProvider;
            set => provider.CurrentProvider = value;
        }

        public event Action<AuthProvider>? OnMethodChanged
        {
            add => provider.OnMethodChanged += value;
            remove => provider.OnMethodChanged -= value;
        }

        public bool IsThirdWebOTP => provider.IsThirdWebOTP;
        public bool IsDappWallet => provider.IsDappWallet;

        public void SetTransactionConfirmationCallback(TransactionConfirmationDelegate? callback) =>
            provider.SetTransactionConfirmationCallback(callback);

        // IDappVerificationHandler
        public event Action<(int code, DateTime expiration, string requestId)>? VerificationRequired
        {
            add => provider.VerificationRequired += value;
            remove => provider.VerificationRequired -= value;
        }

        public void CancelCurrentWeb3Operation() =>
            provider.CancelCurrentWeb3Operation();

        // IOtpAuthenticator
        public UniTask SubmitOtpAsync(string otp, CancellationToken ct = default) =>
            provider.SubmitOtpAsync(otp, ct);

        public UniTask ResendOtpAsync(CancellationToken ct = default) =>
            provider.ResendOtpAsync(ct);

        public async UniTask<bool> TryAutoLoginAsync(CancellationToken ct) =>
            await provider.TryAutoLoginAsync(ct);

        // IWeb3Authenticator
        public async UniTask<IWeb3Identity> LoginAsync(LoginPayload payload, CancellationToken ct)
        {
            IWeb3Identity identity = await provider.LoginAsync(payload, ct);
            identityCache.Identity = identity;
            return identity;
        }

        public async UniTask LogoutAsync(CancellationToken ct)
        {
            await provider.LogoutAsync(ct);
            identityCache.Clear();
        }

        // IEthereumApi
        public UniTask<EthApiResponse> SendAsync(EthApiRequest request, Web3RequestSource source, CancellationToken ct) =>
            provider.SendAsync(request, source, ct);

        public void Dispose()
        {
            provider.Dispose();
            identityCache.Dispose();
        }
    }
}
