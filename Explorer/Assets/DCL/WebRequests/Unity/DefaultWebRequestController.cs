using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.RequestsHub;
using System;
using System.Threading;
using UnityEngine.Networking;
using Utility.Multithreading;

namespace DCL.WebRequests
{
    public class DefaultWebRequestController : IWebRequestController
    {
        private readonly IWebRequestsAnalyticsContainer analyticsContainer;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IRequestHub requestHub;

        public DefaultWebRequestController(IWebRequestsAnalyticsContainer analyticsContainer, IWeb3IdentityCache web3IdentityCache, IRequestHub requestHub)
        {
            this.analyticsContainer = analyticsContainer;
            this.web3IdentityCache = web3IdentityCache;
            this.requestHub = requestHub;
        }

        public async UniTask<IWebRequest> SendAsync(ITypedWebRequest requestWrap, CancellationToken ct)
        {
            await using ExecuteOnMainThreadScope scope = await ExecuteOnMainThreadScope.NewScopeWithReturnOnOriginalThreadAsync();

            RequestEnvelope envelope = requestWrap.Envelope;

            int attemptsLeft = envelope.CommonArguments.AttemptsCount;

            // ensure disposal of headersInfo
            using ITypedWebRequest _ = requestWrap;

            var analytics = new DefaultWebRequestAnalytics();

            while (attemptsLeft > 0)
            {
                UnityWebRequest nativeRequest = requestWrap.CreateUnityWebRequest();
                var adapter = new DefaultWebRequest(nativeRequest);

                envelope.InitializedWebRequest(web3IdentityCache, adapter);

                try
                {
                    await ExecuteWithAnalytics(requestWrap, adapter, analytics, ct);
                }
                catch (UnityWebRequestException exception)
                {
                    attemptsLeft--;

                    if (!envelope.SuppressErrors)

                        // Print verbose
                        ReportHub.LogError(
                            envelope.ReportData,
                            $"Exception occured on loading {requestWrap.GetType().Name} from {envelope.CommonArguments.URL} with args {requestWrap.ArgsToString()},\n with {envelope}\n"
                        );

                    if (exception.Message.Contains(WebRequestUtils.CANNOT_CONNECT_ERROR))
                    {
                        // TODO: (JUANI) From time to time we can get several curl errors that need a small delay to recover
                        // This can be removed if we solve the issue with Unity
                        await UniTask.Delay(TimeSpan.FromSeconds(0.5f));
                    }

                    if (envelope.CommonArguments.AttemptsDelayInMilliseconds() > 0)
                        await UniTask.Delay(TimeSpan.FromMilliseconds(envelope.CommonArguments.AttemptsDelayInMilliseconds()));

                    if (exception.IsIrrecoverableError(adapter, attemptsLeft))
                    {
                        adapter.Dispose();
                        throw;
                    }

                    if (attemptsLeft > 0)

                        // Dispose the previous request before making a new attempt
                        adapter.Dispose();
                }
            }

            throw new Exception($"{nameof(DefaultWebRequestController)}: Unexpected code path!");
        }

        private async UniTask ExecuteWithAnalytics(ITypedWebRequest request, DefaultWebRequest adapter, DefaultWebRequestAnalytics analytics, CancellationToken ct)
        {
            var requestFinished = false;

            analytics.OnStarted(adapter.unityWebRequest);

            try
            {
                analyticsContainer.OnRequestStarted(request, adapter, analytics);

                await UniTask.WhenAny(adapter.unityWebRequest.SendWebRequest().WithCancellation(ct), UpdateAnalytics());

                requestFinished = true;

                async UniTask UpdateAnalytics()
                {
                    while (!ct.IsCancellationRequested && !requestFinished)
                    {
                        analytics.Update();
                        await UniTask.Yield();
                    }
                }
            }
            finally { analyticsContainer.OnRequestFinished(request, adapter, analytics); }
        }

        IRequestHub IWebRequestController.requestHub => requestHub;
    }
}
