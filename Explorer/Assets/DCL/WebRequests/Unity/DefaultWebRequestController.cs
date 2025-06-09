using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.RequestsHub;
using Sentry;
using System;
using System.Threading;
using UnityEngine.Assertions;
using UnityEngine.Networking;
using Utility.Multithreading;

namespace DCL.WebRequests
{
    public class DefaultWebRequestController : IWebRequestController
    {
        private readonly IWebRequestsAnalyticsContainer analyticsContainer;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IRequestHub requestHub;

        IRequestHub IWebRequestController.requestHub => requestHub;

        public DefaultWebRequestController(IWebRequestsAnalyticsContainer analyticsContainer, IWeb3IdentityCache web3IdentityCache, IRequestHub requestHub)
        {
            this.analyticsContainer = analyticsContainer;
            this.web3IdentityCache = web3IdentityCache;
            this.requestHub = requestHub;
        }

        public async UniTask<IWebRequest> SendAsync(ITypedWebRequest requestWrap, bool detachDownloadHandler, CancellationToken ct)
        {
            await using ExecuteOnMainThreadScope scope = await ExecuteOnMainThreadScope.NewScopeWithReturnOnOriginalThreadAsync();

            RequestEnvelope envelope = requestWrap.Envelope;

            int attemptsLeft = envelope.CommonArguments.AttemptsCount;

            DefaultWebRequest? adapter = null;

            TimeSpan delayBeforeRepeat = TimeSpan.Zero;

            while (attemptsLeft > 0)
            {
                try
                {
                    if (delayBeforeRepeat != TimeSpan.Zero)
                        await UniTask.Delay(delayBeforeRepeat, cancellationToken: ct);

                    UnityWebRequest nativeRequest = requestWrap.CreateUnityWebRequest();

                    // In reality the life-cycle of WRs is always closed. Uncomment this line and introduce individual dispose if required in the future
                    // nativeRequest.disposeDownloadHandlerOnDispose = !detachDownloadHandler;

                    adapter = new DefaultWebRequest(nativeRequest, requestWrap);

                    envelope.OnCreated?.Invoke(adapter);

                    envelope.InitializedWebRequest(web3IdentityCache, adapter);

                    await ExecuteWithAnalyticsAsync(requestWrap, adapter, ct);

                    adapter.successfullyExecutedByController = true;
                    return adapter;
                }
                catch (UnityWebRequestException exception)
                {
                    Assert.IsNotNull(adapter);

                    attemptsLeft--;

                    if (!envelope.SuppressErrors)

                        // Print verbose
                        ReportHub.LogError(
                            envelope.ReportData,
                            $"Exception occured on loading {requestWrap.GetType().Name} from {envelope.CommonArguments.URL} with args {requestWrap.ArgsToString()},\n with {envelope}\n{exception}"
                        );

                    if (exception.IsIrrecoverableError(adapter!, attemptsLeft))
                    {
                        SentrySdk.AddBreadcrumb($"Irrecoverable exception occured on loading {requestWrap.GetType().Name} from {envelope.CommonArguments.URL} with {envelope}", level: BreadcrumbLevel.Info);

                        var adaptedException = new DefaultWebRequestException(adapter!, exception);
                        adapter!.Dispose();
                        throw adaptedException;
                    }

                    if (exception.Message.Contains(WebRequestUtils.CANNOT_CONNECT_ERROR))

                        // TODO: (JUANI) From time to time we can get several curl errors that need a small delay to recover
                        // This can be removed if we solve the issue with Unity
                        delayBeforeRepeat = TimeSpan.FromSeconds(0.5f);

                    else if (envelope.CommonArguments.AttemptsDelayInMilliseconds() > 0)
                        delayBeforeRepeat = TimeSpan.FromMilliseconds(envelope.CommonArguments.AttemptsDelayInMilliseconds());

                    // Dispose of the previous native request before repeating
                    adapter!.unityWebRequest.Dispose();
                }
                catch (Exception) // any other exception
                {
                    // Dispose adapter if it was created or the wrap on exception as it won't be returned to the caller
                    adapter?.Dispose();
                    throw;
                }
            }

            throw new Exception($"{nameof(DefaultWebRequestController)}: Unexpected code path!");
        }

        private async UniTask ExecuteWithAnalyticsAsync(ITypedWebRequest request, DefaultWebRequest adapter, CancellationToken ct)
        {
            UnityWebRequest uwr = adapter.unityWebRequest;
            var parallelFlowCts = new CancellationTokenSource();

            try
            {
                analyticsContainer.OnRequestStarted(request, adapter);

                UpdateAdapterAsync(adapter, parallelFlowCts.Token).Forget();

                await uwr.SendWebRequest().WithCancellation(ct);

                // Updating every frame is necessary to converge the API
                static async UniTaskVoid UpdateAdapterAsync(DefaultWebRequest adapter, CancellationToken ct)
                {
                    while (!ct.IsCancellationRequested)
                    {
                        adapter.Update();
                        await UniTask.Yield();
                    }
                }
            }
            finally
            {
                parallelFlowCts.Cancel();
                parallelFlowCts.Dispose();
                analyticsContainer.OnRequestFinished(request, adapter);
            }
        }

        public void Dispose() { }
    }
}
