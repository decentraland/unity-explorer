using DCL.Web3.Authenticators;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class AnalyticsDecoratorVerifiedAuthenticator : AnalyticsDecoratorAuthenticator, IWeb3VerifiedAuthenticator
    {
        private readonly IWeb3VerifiedAuthenticator core;

        public AnalyticsDecoratorVerifiedAuthenticator(IWeb3VerifiedAuthenticator core, IAnalyticsController analytics)
            : base(core, analytics)
        {
            this.core = core;
        }

        public void SetVerificationListener(IWeb3VerifiedAuthenticator.VerificationDelegate? callback) =>
            core.SetVerificationListener(callback);

        public void SetOtpRequestListener(IWeb3VerifiedAuthenticator.OtpRequestDelegate? callback) =>
            core.SetOtpRequestListener(callback);
    }
}
