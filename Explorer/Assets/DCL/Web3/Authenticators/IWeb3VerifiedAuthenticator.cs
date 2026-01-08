using System;

namespace DCL.Web3.Authenticators
{
    public interface IWeb3VerifiedAuthenticator : IWeb3Authenticator
    {
        /// <summary>
        /// Raised when verification code should be displayed to the user.
        /// Always invoked on the main thread.
        /// </summary>
        event Action<(int code, DateTime expiration, string requestId)>? VerificationRequired;

        /// <summary>
        ///     Raised when OTP input is required from user (ThirdWeb flow).
        /// </summary>
        event Action? OtpRequired;

        void CancelCurrentWeb3Operation();

        /// <summary>
        /// Submit OTP code entered by user.
        /// </summary>
        void SubmitOtp(string otp);
    }
}
