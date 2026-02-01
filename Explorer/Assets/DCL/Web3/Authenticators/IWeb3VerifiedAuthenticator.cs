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
        ///     Submit OTP code entered by user.
        ///     Throws <see cref="OtpValidationException"/> if code is invalid/expired.
        /// </summary>
        UniTask SubmitOtp(string otp);

        /// <summary>
        ///     Resend OTP code to the same email.
        ///     Can only be called during active login session.
        /// </summary>
        UniTask ResendOtp();

        public UniTask<bool> TryAutoLoginAsync(CancellationToken ct);
    }
}
