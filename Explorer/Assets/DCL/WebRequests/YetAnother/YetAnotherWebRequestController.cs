using Cysharp.Net.Http;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.RequestsHub;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.WebRequests
{
    public class YetAnotherWebRequestController : IWebRequestController
    {
        private readonly HttpClient httpClient;
        private readonly IWebRequestsAnalyticsContainer analyticsContainer; // TODO
        private readonly IWeb3IdentityCache identityCache;
        private readonly IRequestHub requestHub;

        IRequestHub IWebRequestController.requestHub => requestHub;

        public YetAnotherWebRequestController(IWebRequestsAnalyticsContainer analyticsContainer, IWeb3IdentityCache identityCache, IRequestHub requestHub)
        {
            this.analyticsContainer = analyticsContainer;
            this.identityCache = identityCache;
            this.requestHub = requestHub;

            var handler = new YetAnotherHttpHandler();
            httpClient = new HttpClient(handler);
        }

        public void Dispose()
        {
            httpClient.Dispose();
        }

        public async UniTask<IWebRequest> SendAsync(ITypedWebRequest requestWrap, bool detachDownloadHandler, CancellationToken ct)
        {
            bool fromMainThread = PlayerLoopHelper.IsMainThread;

            if (fromMainThread)
                await UniTask.SwitchToThreadPool();

            RequestEnvelope envelope = requestWrap.Envelope;

            int attemptsLeft = envelope.CommonArguments.AttemptsCount;

            YetAnotherWebRequest? adapter = null;

            TimeSpan delayBeforeRepeat = TimeSpan.Zero;

            while (attemptsLeft > 0)
            {
                try
                {
                    if (delayBeforeRepeat != TimeSpan.Zero)
                        await UniTask.Delay(delayBeforeRepeat, cancellationToken: ct);

                    HttpRequestMessage nativeRequest = requestWrap.CreateYetAnotherHttpRequest();
                    adapter = new YetAnotherWebRequest(nativeRequest, requestWrap);
                    envelope.OnCreated?.Invoke(adapter);

                    envelope.InitializedWebRequest(identityCache, adapter);

                    // TODO Timeout per request configuration: how to split it for Receiving headers and getting the body?
                    // TODO analytics

                    HttpResponseMessage? response = await httpClient.SendAsync(nativeRequest, HttpCompletionOption.ResponseHeadersRead, ct);

                    // HttpClient will not throw an exception for non-success status codes, so we need to throw an exception manually
                    if (!response.IsSuccessStatusCode)
                        throw new HttpRequestException(response.ReasonPhrase);

                    Stream? stream = await response.Content.ReadAsStreamAsync();

                    // Adapt the stream for compatibility with the existing cache and logic
                    var adaptedStream = new AdaptedDownloadContentStream(stream);

                    adapter.SetResponse(response, adaptedStream);
                    return adapter;
                }
                catch (TaskCanceledException e) when (!ct.IsCancellationRequested)
                {
                    // Timeout
                    if (adapter != null)
                        adapter.IsTimedOut = true;

                    if (adapter?.response != null)
                        adapter.response.Error = "The request timed out.";

                    throw new YetAnotherHttpWebRequestException(adapter, new TimeoutException("The request timed out.", e));
                }
                catch (HttpRequestException exception)
                {
                    attemptsLeft--;

                    if (adapter!.response != null)
                        adapter.response.Error = exception.Message;

                    if (!envelope.SuppressErrors)

                        // Print verbose
                        ReportHub.LogError(
                            envelope.ReportData,
                            $"Exception occured on loading {requestWrap.GetType().Name} from {envelope.CommonArguments.URL} with args {requestWrap.ArgsToString()},\n with {envelope}\n{exception}"
                        );

                    if (adapter.IsIrrecoverableError(attemptsLeft))
                    {
                        var adaptedException = new YetAnotherHttpWebRequestException(adapter, exception);

                        // Dispose adapter on exception as it won't be returned to the caller
                        adapter.Dispose();
                        throw adaptedException;
                    }

                    if (envelope.CommonArguments.AttemptsDelayInMilliseconds() > 0)
                        delayBeforeRepeat = TimeSpan.FromMilliseconds(envelope.CommonArguments.AttemptsDelayInMilliseconds());

                    // Dispose of the previous native request before repeating
                    adapter.response?.Dispose();
                }
                catch (Exception) // any other exception
                {
                    // Dispose adapter on exception as it won't be returned to the caller
                    adapter.Dispose();
                    throw;
                }
                finally
                {
                    if (fromMainThread)
                        await UniTask.SwitchToMainThread();
                }
            }

            throw new Exception($"{nameof(YetAnotherWebRequestController)}: Unexpected code path!");
        }
    }
}
