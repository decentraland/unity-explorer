using DCL.PerformanceAndDiagnostics;
using MVC;
using System;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;

namespace DCL.AuthenticationScreenFlow
{
    public abstract class AuthStateBase : IExitableState
    {
        protected const int STATE_SPAN_DEPTH = 1;

        protected readonly AuthenticationScreenView viewInstance;

        protected AuthStateBase(AuthenticationScreenView viewInstance)
        {
            this.viewInstance = viewInstance;
        }

        protected void Enter()
        {
            SentryTransactionNameMapping.Instance.StartSpan(LOADING_TRANSACTION_NAME, new SpanData
            {
                SpanName = GetDefaultSpanName(),
                SpanOperation = "auth.state",
                Depth = STATE_SPAN_DEPTH,
            });
        }

        public virtual void Exit()
        {
            SentryTransactionNameMapping.Instance.EndSpanOnDepth(LOADING_TRANSACTION_NAME, STATE_SPAN_DEPTH);
        }

        private string GetDefaultSpanName()
        {
            const string SUFFIX = "AuthState";

            string typeName = GetType().Name;

            return typeName.EndsWith(SUFFIX, StringComparison.Ordinal)
                ? typeName[..^SUFFIX.Length]
                : typeName;
        }
    }
}
