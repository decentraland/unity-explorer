using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.Web3.Authenticators
{
    /// <summary>
    ///     Interface for OTP (One-Time Password) authentication flow.
    ///     Used when user authenticates via email + OTP code (ThirdWeb).
    /// </summary>
    public interface IOtpAuthenticator
    {
        /// <summary>
        ///     Raised when OTP code input should be displayed to the user.
        ///     Always invoked on the main thread.
        /// </summary>
        public event Action<string>? OTPSendSucceeded;
        /// <summary>
        ///     Submit OTP code entered by user.
        ///     Throws <see cref="CodeVerificationException" /> if code is invalid/expired.
        /// </summary>
        public UniTask SubmitOtpAsync(string otp, CancellationToken ct = default);

        /// <summary>
        ///     Resend OTP code to the same email.
        ///     Can only be called during active login session.
        /// </summary>
        public UniTask ResendOtpAsync(CancellationToken ct = default);

        /// <summary>
        ///     Attempts to auto-login using stored session.
        ///     Returns true if auto-login succeeded.
        /// </summary>
        public UniTask<bool> TryAutoLoginAsync(CancellationToken ct);
    }
}
