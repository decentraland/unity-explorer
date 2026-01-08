using Cysharp.Threading.Tasks;
using DCL.Web3.Identities;
using System;

namespace DCL.Web3.Authenticators
{
    public class ProxyVerifiedWeb3Authenticator : ProxyWeb3Authenticator, IWeb3VerifiedAuthenticator
    {
        private readonly IWeb3VerifiedAuthenticator authenticator;

        public event Action<(int code, DateTime expiration, string requestId)>? VerificationRequired
        {
            add => authenticator.VerificationRequired += value;
            remove => authenticator.VerificationRequired -= value;
        }

        public ProxyVerifiedWeb3Authenticator(
            IWeb3VerifiedAuthenticator authenticator,
            IWeb3IdentityCache identityCache)
            : base(authenticator, identityCache)
        {
            this.authenticator = authenticator;
        }

        public void CancelCurrentWeb3Operation()
        {
            authenticator.CancelCurrentWeb3Operation();
        }

        public UniTask SubmitOtp(string otp) =>
            authenticator.SubmitOtp(otp);

        public UniTask ResendOtp() =>
            authenticator.ResendOtp();
    }
}
