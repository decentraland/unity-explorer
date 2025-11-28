using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.Web3.Authenticators
{
    public interface IWeb3VerifiedAuthenticator : IWeb3Authenticator
    {
        /// <summary>
        ///     Callback for displaying verification code to user (Magic/Dapp flow - push-based)
        /// </summary>
        public delegate void VerificationDelegate(int code, DateTime expiration, string requestID);

        /// <summary>
        ///     Callback for requesting OTP input from user (ThirdWeb flow - pull-based)
        /// </summary>
        public delegate UniTask<string> OtpRequestDelegate(CancellationToken ct);

        public void SetVerificationListener(VerificationDelegate? callback);

        public void SetOtpRequestListener(OtpRequestDelegate? callback);
    }
}
