using DCL.Web3.Authenticators;
using System;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class AnalyticsDecoratorVerifiedAuthenticator : AnalyticsDecoratorAuthenticator, IWeb3VerifiedAuthenticator
    {
        private readonly IWeb3VerifiedAuthenticator core;

        public event Action<(int code, DateTime expiration, string requestId)>? VerificationRequired
        {
            add => core.VerificationRequired += value;
            remove => core.VerificationRequired -= value;
        }

        public event Action? OtpRequired
        {
            add => core.OtpRequired += value;
            remove => core.OtpRequired -= value;
        }

        public AnalyticsDecoratorVerifiedAuthenticator(IWeb3VerifiedAuthenticator core, IAnalyticsController analytics)
            : base(core, analytics)
        {
            this.core = core;
        }

        public void CancelCurrentWeb3Operation()
        {
            core.CancelCurrentWeb3Operation();
        }

        public void SubmitOtp(string otp) =>
            core.SubmitOtp(otp);
    }
}
