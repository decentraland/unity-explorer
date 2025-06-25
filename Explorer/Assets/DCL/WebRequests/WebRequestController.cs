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
        private readonly IWebRequestsAnalyticsContainer analyticsContainer;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IRequestHub requestHub;

        private static readonly ThreadLocal<StringBuilder> BREADCRUMB_BUILDER = new (() => new StringBuilder(150));

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

            int attemptsLeft = envelope.CommonArguments.TotalAttempts();

            // ensure disposal of headersInfo
            using RequestEnvelope<TWebRequest, TWebRequestArgs> _ = envelope;

            while (attemptsLeft > 0)
            {
                TWebRequest request = envelope.InitializedWebRequest(web3IdentityCache);

                // No matter what we must release UnityWebRequest, otherwise it crashes in the destructor
                using UnityWebRequest wr = request.UnityWebRequest;

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

                    if (!envelope.SuppressErrors)

                        // Print verbose
                        ReportHub.LogError(
                            envelope.ReportData,
                            $"Exception occured on loading {typeof(TWebRequest).Name} from {envelope.CommonArguments.URL} with {envelope}\n"
                            + $"Attempt Left: {attemptsLeft}"
                        );

                    if (exception.Message.Contains(WebRequestUtils.CANNOT_CONNECT_ERROR))
                    {
                        // TODO: (JUANI) From time to time we can get several curl errors that need a small delay to recover
                        // This can be removed if we solve the issue with Unity
                        await UniTask.Delay(TimeSpan.FromSeconds(0.5f));
                    }

                    if (envelope.CommonArguments.AttemptsDelayInMilliseconds() > 0)
                        await UniTask.Delay(TimeSpan.FromMilliseconds(envelope.CommonArguments.AttemptsDelayInMilliseconds()));


                    if (exception.IsIrrecoverableError(attemptsLeft) && !envelope.IgnoreIrrecoverableErrors)
                    {
                        // Ignore the file error as we always try to read from the file first
                        if (!envelope.CommonArguments.URL.Value.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                            SentrySdk.AddBreadcrumb($"Irrecoverable exception occured on executing {envelope.GetBreadcrumbString(BREADCRUMB_BUILDER.Value)}", level: BreadcrumbLevel.Info);

                        throw;
                    }
                }
            }

            throw new Exception($"{nameof(WebRequestController)}: Unexpected code path!");
        }

        IRequestHub IWebRequestController.requestHub => requestHub;
    }
}
