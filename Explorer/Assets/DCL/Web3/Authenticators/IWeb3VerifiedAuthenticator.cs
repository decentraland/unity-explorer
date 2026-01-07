using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.Web3.Authenticators
{
    public interface IWeb3VerifiedAuthenticator : IWeb3Authenticator
    {
        /// <summary>
        /// Raised when verification code should be displayed to the user.
        /// Always invoked on the main thread.
        /// </summary>
        event Action<(int code, DateTime expiration, string requestId)>? VerificationRequired;

        void CancelCurrentWeb3Operation();
        /// <summary>
        ///     Callback for requesting OTP input from user (ThirdWeb flow - pull-based)
        /// </summary>
        public delegate UniTask<string> OtpRequestDelegate(CancellationToken ct);

        public void SetOtpRequestListener(OtpRequestDelegate? callback);
    }
}
