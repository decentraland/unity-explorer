using Cysharp.Threading.Tasks;
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
        ///     Submit OTP code entered by user.
        ///     Throws <see cref="CodeVerificationException" /> if code is invalid/expired.
        /// </summary>
        public UniTask SubmitOtp(string otp);

        /// <summary>
        ///     Resend OTP code to the same email.
        ///     Can only be called during active login session.
        /// </summary>
        public UniTask ResendOtp();

        /// <summary>
        ///     Attempts to auto-login using stored session.
        ///     Returns true if auto-login succeeded.
        /// </summary>
        public UniTask<bool> TryAutoLoginAsync(CancellationToken ct);
    }
}
