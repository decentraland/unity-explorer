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

        public async UniTask<TWebRequest> SendAsync<TWebRequest, TWebRequestArgs>(RequestEnvelope<TWebRequest, TWebRequestArgs> envelope)
            where TWebRequestArgs: struct
            where TWebRequest: struct, ITypedWebRequest
        {
            int attemptsLeft = envelope.CommonArguments.TotalAttempts();

            // ensure disposal of headersInfo
            using var _ = envelope;

            while (attemptsLeft > 0)
            {
                TWebRequest request = envelope.InitializedWebRequest(web3IdentityCache);
                var exceptionThrown = false;

                try
                {
                    analyticsContainer.OnRequestStarted(request);
                    await request.SendRequest(envelope.Ct);
                    // if no exception is thrown Request is successful
                    return request;
                }
                catch (UnityWebRequestException exception)
                {
                    exceptionThrown = true;
                    attemptsLeft--;

                    // Print verbose
                    ReportHub.LogError(envelope.ReportCategory, $"Exception occured on loading {typeof(TWebRequest).Name} from {envelope.CommonArguments.URL}.\n"
                                                       + $"Attempt Left: {attemptsLeft}");

                    if (exception.IsIrrecoverableError(attemptsLeft))
                        throw;
                }
                finally
                {
                    analyticsContainer.OnRequestFinished(request);

                    if (exceptionThrown)
                    {
                        // Make the request no longer usable as all data needed is written into the exception itself
                        request.UnityWebRequest.Dispose();
                    }
                }
            }

            throw new Exception($"{nameof(WebRequestController)}: Unexpected code path!");
        }
    }
}
