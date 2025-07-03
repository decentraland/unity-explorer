using Utility.Multithreading;
using Cysharp.Net.Http;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.RequestsHub;
using Sentry;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DCL.WebRequests
{
    public class YetAnotherWebRequestController : IWebRequestController
    {
        private static readonly string USER_AGENT = $"Yet Another Web Request Controller/Unity {Application.unityVersion}";

        private readonly HttpClient httpClient;
        private readonly IWebRequestsAnalyticsContainer analyticsContainer;
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
            await using ExecuteOnThreadPoolScope _ = await ExecuteOnThreadPoolScope.NewScopeAsync();

            RequestEnvelope envelope = requestWrap.Envelope;

            int attemptsLeft = envelope.CommonArguments.AttemptsCount;

            YetAnotherWebRequest? adapter = null;

            TimeSpan delayBeforeRepeat = TimeSpan.Zero;

            while (attemptsLeft > 0)
            {
                try
                {
                    if (delayBeforeRepeat != TimeSpan.Zero)
                        await Task.Delay(delayBeforeRepeat, ct);

                    (HttpRequestMessage nativeRequest, ulong uploadSize) = requestWrap.CreateYetAnotherHttpRequest();

                    // Some APIs require a User-Agent header to be set, hyper doesn't set it by default
                    nativeRequest.Headers.Add("User-Agent", USER_AGENT);

                    adapter = new YetAnotherWebRequest(nativeRequest, requestWrap);
                    envelope.OnCreated?.Invoke(adapter);

                    envelope.InitializedWebRequest(identityCache, adapter);

                    analyticsContainer.OnRequestStarted(requestWrap, adapter);

                    // TODO Timeout per request configuration: how to split it for Receiving headers and getting the body? Do we ever need it?
                    try
                    {
                        HttpResponseMessage response = await httpClient.SendAsync(nativeRequest, HttpCompletionOption.ResponseHeadersRead, ct);

                        // We must handle redirections manually as hyper doesn't support them automatically
                        while (response.IsRedirected())
                        {
                            Uri? lastUri = nativeRequest.RequestUri;

                            nativeRequest.Dispose();
                            response.Dispose();

                            (nativeRequest, uploadSize) = requestWrap.CreateYetAnotherHttpRequest();
                            nativeRequest.RequestUri = response.Headers.Location;
                            nativeRequest.Headers.Referrer = lastUri;

                            adapter.SetRedirected(nativeRequest);

                            response = await httpClient.SendAsync(nativeRequest, HttpCompletionOption.ResponseHeadersRead, ct);
                        }

                        adapter.UploadedBytes = uploadSize;

                        Stream? stream = await response.Content.ReadAsStreamAsync();

                        var headers = new WebRequestHeaders(response);

                        // Adapt the stream for compatibility with the existing cache and logic
                        var adaptedStream = new YetAnotherDownloadContentStream(stream);
                        YetAnotherWebResponse adaptedResponse = adapter.SetResponse(response, headers, adaptedStream);

                        // HttpClient will not throw an exception for non-success status codes, so we need to throw an exception manually
                        if (!response.IsSuccessStatusCode)
                        {
                            // Stream will contain the error response body
                            string error = await adaptedResponse.GetTextAsync(ct);
                            var exceptionMessage = $"{nativeRequest.Method} {nativeRequest.RequestUri}, {(int)response.StatusCode}: {response.ReasonPhrase}";

                            adaptedResponse.Error = string.IsNullOrEmpty(error) ? exceptionMessage : error;

                            throw new HttpRequestException($"{nativeRequest.Method} {nativeRequest.RequestUri}, {(int)response.StatusCode}: {response.ReasonPhrase}\n{error}");
                        }

                        adapter.successfullyExecutedByController = true;
                    }
                    finally
                    {
                        // Analytics must be called when the object is not disposed
                        // TODO despite the request is finished here, the data can be still processed asynchronously
                        // The analytics approach must be re-thought
                        analyticsContainer.OnRequestFinished(requestWrap, adapter);
                    }

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

                // YetAnother throws different exceptions depending on the error, so we catch all exceptions here, see ResponseContext.cs
                catch (Exception exception) when (exception is HttpRequestException or IOException or { InnerException: IOException })
                {
                    attemptsLeft--;

                    if (!envelope.SuppressErrors)

                        // Print verbose
                        ReportHub.LogError(
                            envelope.ReportData,
                            $"Exception occured on loading {requestWrap.GetType().Name} from {envelope.CommonArguments.URL} with args {requestWrap.ArgsToString()},\n with {envelope}\n{exception}"
                        );

                    if (adapter!.IsIrrecoverableError(attemptsLeft))
                    {
                        IWebRequestController.AddFailedBreadcrumb(in envelope);

                        var adaptedException = new YetAnotherHttpWebRequestException(adapter!, exception);

                        // Dispose adapter on exception as it won't be returned to the caller
                        adapter!.Dispose();
                        throw adaptedException;
                    }

                    if (envelope.CommonArguments.AttemptsDelayInMilliseconds() > 0)
                        delayBeforeRepeat = TimeSpan.FromMilliseconds(envelope.CommonArguments.AttemptsDelayInMilliseconds());

                    // Dispose of the previous native request before repeating
                    adapter!.Dispose();
                }
                catch (Exception) // any other exception
                {
                    // Dispose adapter on exception as it won't be returned to the caller
                    adapter?.Dispose();
                    throw;
                }
            }

            throw new Exception($"{nameof(YetAnotherWebRequestController)}: Unexpected code path!");
        }
    }
}
