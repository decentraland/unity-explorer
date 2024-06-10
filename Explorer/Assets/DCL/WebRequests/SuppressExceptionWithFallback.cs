using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using System.Threading;

namespace DCL.WebRequests
{
    /// <summary>
    ///     Wraps <see cref="TCoreOp" /> in try...catch and returns the default value on exception
    /// </summary>
    public readonly struct SuppressExceptionWithFallback<TCoreOp, TWebRequest, TResult>
        : IWebRequestOp<TWebRequest, TResult> where TWebRequest: struct, ITypedWebRequest
                                              where TCoreOp: IWebRequestOp<TWebRequest, TResult>
    {
        private readonly TCoreOp coreOp;
        private readonly TResult fallbackValue;
        private readonly SuppressExceptionWithFallback.Behaviour behaviour;
        private readonly ReportData? reportContext;

        public SuppressExceptionWithFallback(TCoreOp coreOp,
            TResult fallbackValue,
            SuppressExceptionWithFallback.Behaviour behaviour = SuppressExceptionWithFallback.Behaviour.Default,
            ReportData? reportContext = null)
        {
            this.coreOp = coreOp;
            this.fallbackValue = fallbackValue;
            this.behaviour = behaviour;
            this.reportContext = reportContext;
        }

        public UniTask<TResult?> ExecuteAsync(TWebRequest webRequest, CancellationToken ct) =>
            coreOp.ExecuteAsync(webRequest, ct).SuppressExceptionWithFallback(fallbackValue, behaviour, reportContext);
    }
}
