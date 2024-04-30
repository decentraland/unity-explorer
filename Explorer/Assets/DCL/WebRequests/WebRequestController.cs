using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using System;

namespace DCL.WebRequests
{
    public class WebRequestController : IWebRequestController
    {
        private readonly IWebRequestsAnalyticsContainer analyticsContainer;
        private readonly IWeb3IdentityCache web3IdentityCache;

        public WebRequestController(IWeb3IdentityCache web3IdentityCache) : this(IWebRequestsAnalyticsContainer.DEFAULT, web3IdentityCache) { }

        public WebRequestController(IWebRequestsAnalyticsContainer analyticsContainer, IWeb3IdentityCache web3IdentityCache)
        {
            this.analyticsContainer = analyticsContainer;
            this.web3IdentityCache = web3IdentityCache;
        }

        public async UniTask<TWebRequestOp> SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp>(RequestEnvelope<TWebRequest, TWebRequestArgs> envelope, TWebRequestOp op)
            where TWebRequestArgs: struct
            where TWebRequest: struct, ITypedWebRequest
            where TWebRequestOp : IWebRequestOp<TWebRequest>
        {
            int attemptsLeft = envelope.CommonArguments.TotalAttempts();

            // ensure disposal of headersInfo
            using var _ = envelope;

            while (attemptsLeft > 0)
            {
                TWebRequest request = envelope.InitializedWebRequest(web3IdentityCache);

                // No matter what we must release UnityWebRequest, otherwise it crashes in the destructor
                using var wr = request.UnityWebRequest;

                try
                {
                    await request.WithAnalytics(analyticsContainer, request.SendRequest(envelope.Ct));

                    // if no exception is thrown Request is successful and the continuation op can be executed
                    await op.ExecuteAsync(request, envelope.Ct);
                    // After the operation is executed, the flow may continue in the background thread
                    return op;
                }
                catch (UnityWebRequestException exception)
                {
                    // The operation will be in an unresolved state in that case
                    if (envelope.ShouldIgnoreResponseError(exception.UnityWebRequest!))
                        return op;

                    attemptsLeft--;

                    // Print verbose
                    ReportHub.LogError(
                        envelope.ReportCategory,
                        $"Exception occured on loading {typeof(TWebRequestOp).Name} from {envelope.CommonArguments.URL} with {envelope}\n"
                        + $"Attempt Left: {attemptsLeft}"
                    );

                    if (exception.IsIrrecoverableError(attemptsLeft))
                        throw;
                }
            }

            throw new Exception($"{nameof(WebRequestController)}: Unexpected code path!");
        }
    }
}
