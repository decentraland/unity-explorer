using DCL.PerformanceAndDiagnostics;
using MVC;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;

namespace DCL.AuthenticationScreenFlow
{
    public abstract class AuthStateBase : IExitableState
    {
        private const int STATE_SPAN_DEPTH = 1;

        protected readonly AuthenticationScreenView viewInstance;

        protected AuthStateBase(AuthenticationScreenView viewInstance)
        {
            this.viewInstance = viewInstance;
        }

        protected void StartSpan(string spanName, string spanOp)
        {
            var web3AuthSpan = new SpanData
            {
                SpanName = spanName,
                SpanOperation = spanOp,
                Depth = STATE_SPAN_DEPTH,
            };

            SentryTransactionNameMapping.Instance.StartSpan(LOADING_TRANSACTION_NAME, web3AuthSpan);
        }

        public virtual void Exit()
        {
            SentryTransactionNameMapping.Instance.EndSpanOnDepth(LOADING_TRANSACTION_NAME, STATE_SPAN_DEPTH);
        }
    }
}
