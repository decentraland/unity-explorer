using System;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities.UIBindings;

namespace DCL.WebRequests
{
    public class DebugMetricsWebRequestController : IWebRequestController
    {
        private readonly IWebRequestController origin;
        private readonly ElementBinding<ulong> requestCannotConnectDebugMetric;
        private readonly ElementBinding<ulong> requestCompleteDebugMetric;


        public DebugMetricsWebRequestController(IWebRequestController origin,
            ElementBinding<ulong> requestCannotConnectDebugMetric, ElementBinding<ulong> requestCompleteDebugMetric)
        {
            this.origin = origin;
            this.requestCannotConnectDebugMetric = requestCannotConnectDebugMetric;
            this.requestCompleteDebugMetric = requestCompleteDebugMetric;
        }

        public async UniTask<TResult?> SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(
            RequestEnvelope<TWebRequest, TWebRequestArgs> envelope, TWebRequestOp op)
            where TWebRequest : struct, ITypedWebRequest
            where TWebRequestArgs : struct
            where TWebRequestOp : IWebRequestOp<TWebRequest, TResult>
        {
            try
            {
                return await origin.SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(envelope, op);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                if (e.Message.Contains(WebRequestUtils.CANNOT_CONNECT_ERROR))
                    requestCannotConnectDebugMetric.Value++;
                throw; // don't re-throw it as a new exception as we loose the original type in that case
            }
            finally
            {
                requestCompleteDebugMetric.Value++;
            }
        }
    }
}