using System;
using DCL.Web3.Authenticators;

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

        public AnalyticsDecoratorVerifiedAuthenticator(IWeb3VerifiedAuthenticator core, IAnalyticsController analytics)
            : base(core, analytics)
        {
            this.core = core;
        }

        public void CancelCurrentWeb3Operation()
        {
            core.CancelCurrentWeb3Operation();
        }
    }
}
