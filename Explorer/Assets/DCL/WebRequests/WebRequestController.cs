using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.SafetyNets;
using System;
using System.Threading;
using UnityEngine.Networking;
using Utility.Multithreading;

namespace DCL.WebRequests
{
    public class WebRequestController : IWebRequestController
    {
        private readonly IWebRequestsAnalyticsContainer analyticsContainer;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly ISafetyNet safetyNet = new SequenceSafetyNet(
            new SharpSafetyNet(),
            new CurlSafetyNet()
        );
        private volatile int count = 0;

        public WebRequestController(IWeb3IdentityCache web3IdentityCache) : this(IWebRequestsAnalyticsContainer.DEFAULT, web3IdentityCache) { }

        public WebRequestController(IWebRequestsAnalyticsContainer analyticsContainer, IWeb3IdentityCache web3IdentityCache)
        {
            this.analyticsContainer = analyticsContainer;
            this.web3IdentityCache = web3IdentityCache;
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
                    int current = Interlocked.Increment(ref count);
                    ReportHub.Log(ReportData.UNSPECIFIED, $"SafetyNet, start request: {current}");

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

                    if ((exception.Message ?? string.Empty).Contains(WebRequestUtils.CANNOT_CONNECT_ERROR))
                    {
                        ReportHub.Log(ReportData.UNSPECIFIED, "SafetyNet execute start");
                        var result = await safetyNet.ExecuteWithStringAsync(request.UnityWebRequest.method, envelope.CommonArguments.URL);

                        if (result.Success)
                            ReportHub.Log(ReportData.UNSPECIFIED, "SafetyNet executed successfully");
                        else
                            ReportHub.LogError(ReportData.UNSPECIFIED, $"SafetyNet failed to execute: {result.ErrorMessage}");

                        throw new Exception($"{nameof(WebRequestController)}: {WebRequestUtils.CANNOT_CONNECT_ERROR}!");

                        // TODO: (JUANI) From time to time we can get several curl errors that need a small delay to recover
                        // This can be removed if we solve the issue with Unity
                        await UniTask.Delay(TimeSpan.FromSeconds(0.5f));
                    }

                    if (exception.IsIrrecoverableError(attemptsLeft))
                        throw;
                }
            }

            throw new Exception($"{nameof(WebRequestController)}: Unexpected code path!");
        }
    }
}
