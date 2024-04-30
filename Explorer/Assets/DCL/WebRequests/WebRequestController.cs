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

        public async UniTask<TResult?> SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(RequestEnvelope<TWebRequest, TWebRequestArgs> envelope, TWebRequestOp op)
            where TWebRequestArgs: struct
            where TWebRequest: struct, ITypedWebRequest
            where TWebRequestOp : IWebRequestOp<TWebRequest, TResult>
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
                    await request.WithAnalyticsAsync(analyticsContainer, request.SendRequest(envelope.Ct));

                    // if no exception is thrown Request is successful and the continuation op can be executed
                    return await op.ExecuteAsync(request, envelope.Ct);
                    // After the operation is executed, the flow may continue in the background thread
                }
                catch (UnityWebRequestException exception)
                {
                    // No result can be concluded in this case
                    if (envelope.ShouldIgnoreResponseError(exception.UnityWebRequest!))
                        return default(TResult);

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
