using System;
using DCL.Web3.Authenticators;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class AnalyticsDecoratorVerifiedAuthenticator : AnalyticsDecoratorAuthenticator
    {
        private readonly IWeb3Authenticator core;

        public AnalyticsDecoratorVerifiedAuthenticator(IWeb3Authenticator core, IAnalyticsController analytics)
            : base(core, analytics)
        {
            this.core = core;
        }
    }
}
