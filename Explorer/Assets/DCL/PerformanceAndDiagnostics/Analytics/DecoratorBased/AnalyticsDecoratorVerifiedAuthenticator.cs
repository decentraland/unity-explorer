using Cysharp.Threading.Tasks;
using DCL.Web3.Authenticators;
using System;
using System.Threading;

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

        public UniTask SubmitOtp(string otp) =>
            core.SubmitOtp(otp);

        public UniTask ResendOtp() =>
            core.ResendOtp();

        public UniTask<bool> TryAutoConnectAsync(CancellationToken ct) =>
            core.TryAutoConnectAsync(ct);
    }
}
