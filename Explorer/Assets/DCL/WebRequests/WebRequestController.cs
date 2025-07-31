using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.RequestsHub;
using Sentry;
using System;
using System.Text;
using System.Threading;
using UnityEngine.Networking;
using Utility.Multithreading;

namespace DCL.WebRequests
{
    public class WebRequestController : IWebRequestController
    {
        private static readonly ThreadLocal<StringBuilder> BREADCRUMB_BUILDER = new (() => new StringBuilder(150));
        private readonly IWebRequestsAnalyticsContainer analyticsContainer;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IRequestHub requestHub;

        IRequestHub IWebRequestController.requestHub => requestHub;

        public WebRequestController(IWebRequestsAnalyticsContainer analyticsContainer, IWeb3IdentityCache web3IdentityCache, IRequestHub requestHub)
        {
            this.analyticsContainer = analyticsContainer;
            this.web3IdentityCache = web3IdentityCache;
            this.requestHub = requestHub;
        }

        public async UniTask<TResult?> SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(RequestEnvelope<TWebRequest, TWebRequestArgs> envelope, TWebRequestOp op)
            where TWebRequestArgs: struct
            where TWebRequest: struct, ITypedWebRequest
            where TWebRequestOp: IWebRequestOp<TWebRequest, TResult>
        {
            await using ExecuteOnMainThreadScope scope = await ExecuteOnMainThreadScope.NewScopeWithReturnOnOriginalThreadAsync();

            RetryPolicy retryPolicy = envelope.CommonArguments.RetryPolicy;
            var attemptNumber = 0;

            // ensure disposal of headersInfo
            using RequestEnvelope<TWebRequest, TWebRequestArgs> _ = envelope;

            while (true)
            {
                TWebRequest request = envelope.InitializedWebRequest(web3IdentityCache);
                bool idempotent = request.IsIdempotent(envelope.signInfo);

                // No matter what we must release UnityWebRequest, otherwise it crashes in the destructor
                using UnityWebRequest wr = request.UnityWebRequest;

                try
                {
                    attemptNumber++;

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

                    if (!envelope.SuppressErrors)

                        // Print verbose
                        ReportHub.LogError(
                            envelope.ReportData,
                            $"Exception (code {exception.ResponseCode}) occured on loading {typeof(TWebRequest).Name} from {envelope.CommonArguments.URL} with {envelope}\n"
                            + $"Attempt: {attemptNumber}/{retryPolicy.maxRetriesCount + 1}"
                        );

                    (bool canBeRepeated, TimeSpan retryDelay) = WebRequestUtils.CanBeRepeated(attemptNumber, retryPolicy, idempotent, exception);

                    if (!canBeRepeated && !envelope.IgnoreIrrecoverableErrors)
                    {
                        // Ignore the file error as we always try to read from the file first
                        if (!envelope.CommonArguments.URL.Value.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                            SentrySdk.AddBreadcrumb($"{envelope.ReportData.Category} | {envelope.ReportData.SceneShortInfo.BaseParcel}: Irrecoverable exception (code {exception.ResponseCode}) occured on executing {envelope.GetBreadcrumbString(BREADCRUMB_BUILDER.Value)}", level: BreadcrumbLevel.Info);

                        throw;
                    }

                    await UniTask.Delay(retryDelay, DelayType.Realtime, cancellationToken: envelope.Ct);
                }
            }
        }
    }
}
