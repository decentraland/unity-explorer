using Best.HTTP;
using Best.HTTP.Caching;
using Best.HTTP.Response;
using Best.HTTP.Shared.Logger;
using Best.HTTP.Shared.PlatformSupport.Memory;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.RequestsHub;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility.Multithreading;

namespace DCL.WebRequests.HTTP2
{
    public class Http2WebRequestController : IWebRequestController
    {
        private readonly IWeb3IdentityCache identityCache;
        private readonly IWebRequestsAnalyticsContainer analyticsContainer;
        private readonly IRequestHub requestHub;
        private readonly HTTPCache cache;
        private readonly long chunkSize;

        IRequestHub IWebRequestController.requestHub => requestHub;

        public Http2WebRequestController(IWebRequestsAnalyticsContainer analyticsContainer, IWeb3IdentityCache identityCache, IRequestHub requestHub, HTTPCache cache, long chunkSize)
        {
            this.identityCache = identityCache;
            this.analyticsContainer = analyticsContainer;
            this.requestHub = requestHub;
            this.cache = cache;
            this.chunkSize = chunkSize;
        }

        public async UniTask<PartialDownloadStream> GetPartialAsync(CommonArguments commonArguments, PartialDownloadArguments partialArgs, CancellationToken ct, WebRequestHeadersInfo? headersInfo = null)
        {
            // If the result is fully cached in the stream that was passed, return it immediately
            var partialStream = partialArgs.Stream as Http2PartialDownloadDataStream;

            if (partialStream is { IsFullyDownloaded: true })
                return partialStream;

            // if the result is already fully cached return it immediately without creating a web request
            if (partialStream == null
                && Http2PartialDownloadDataStream.TryInitializeFromCache(cache, HTTPCache.CalculateHash(HTTPMethods.Get, new Uri(commonArguments.URL)), out partialStream)
                && partialStream!.IsFullyDownloaded)
                return partialStream;

            // Create headers accordingly, at this point don't create a partial stream as we don't know if the endpoint actually supports "Range" requests
            long chunkStart = partialStream?.partialContentLength ?? 0;
            long chunkEnd = partialStream == null ? chunkSize : Math.Min(partialStream.fullFileSize - 1, chunkStart + chunkSize);

            headersInfo = (headersInfo ?? new WebRequestHeadersInfo()).WithRange(chunkStart, chunkEnd);

            var partialFlowCompletionSource = new UniTaskCompletionSource();
            var partialFlowCts = new CancellationTokenSource();

            // Create a get web request
            using PartialDownloadRequest request = this.Create<PartialDownloadRequest, PartialDownloadArguments>(partialArgs, commonArguments, ReportCategory.PARTIAL_LOADING, headersInfo,
                onRequestCreated: request =>
                {
                    var nativeRequest = (HTTPRequest)request.nativeRequest;

                    // Default Cache should be disabled as Http2PartialDownloadDataStream both partial and non-partial requests
                    nativeRequest.DownloadSettings.DisableCache = true;

                    // Warning: events are delayed, a data copy is passed to events (such as headers, may allocate heavily)
                    // But response.Headers are not threadsafe =-( so we have to use a copy to avoid random `null` errors

                    nativeRequest.DownloadSettings.OnHeadersReceived += (_, _, headers) => { PartialFlowAsync(nativeRequest, headers, partialFlowCts.Token).Forget(); };

                    async UniTask PartialFlowAsync(HTTPRequest request, Dictionary<string, List<string>> headers, CancellationToken ct)
                    {
                        try
                        {
                            await UniTask.SwitchToThreadPool();

                            if (ct.IsCancellationRequested)
                                return;

                            // Check headers
                            // If at any stage headers are not correct discard the partial stream and the cached result, as the server does not support any headers
                            if (!Http2PartialDownloadDataStream.TryInitializeFromHeaders(cache, request, headers, ref partialStream, out int expectedChunkLength))
                            {
                                partialStream?.DiscardAndDispose();
                                partialStream = Http2PartialDownloadDataStream.InitializeFromUnknownSource();
                            }

                            long startPartialLength = partialStream!.partialContentLength;

                            // Wait for incoming data
                            DownloadContentStream downStream;

                            while ((downStream = request.Response.DownStream) == null)
                            {
                                if (ct.IsCancellationRequested)
                                    return;

                                await UniTask.Yield();
                            }

                            LoggingContext? loggingContext = request.Context;

                            // Keep non-blocking reading from the download stream

                            while (!downStream.IsCompleted)
                            {
                                if (ct.IsCancellationRequested)
                                    return;

                                if (downStream.TryTake(out BufferSegment segment))
                                    if (!partialStream!.TryAppend(segment, loggingContext))
                                    {
                                        downStream.Dispose();
                                        throw CreateException($"{nameof(Http2PartialDownloadDataStream.TryAppend)} failed");
                                    }

                                await UniTask.Yield();
                            }

                            // Check that enough data was downloaded
                            if (expectedChunkLength != 0)
                            {
                                var downloadLength = (int)(partialStream.partialContentLength - startPartialLength);

                                if (downloadLength != expectedChunkLength)
                                    throw CreateException($"Expected to load {expectedChunkLength} bytes, but loaded {downloadLength} bytes");
                            }

                            partialFlowCompletionSource.TrySetResult();
                        }
                        catch (Exception e)
                        {
                            partialStream?.DiscardAndDispose();
                            partialFlowCompletionSource.TrySetException(e);
                        }

                        Exception CreateException(string message) =>
                            new ($"Exception occured in {nameof(PartialFlowAsync)} {request.Uri}: {message}");
                    }
                });

            try
            {
                static async UniTask WaitForRequest(PartialDownloadRequest request, CancellationToken ct)
                {
                    using IWebRequest? createdRequest = await request.SendAsync(ct);
                }

                await UniTask.WhenAll(WaitForRequest(request, ct), partialFlowCompletionSource.Task);
            }
            catch (Exception e)
            {
                partialFlowCts.Cancel();
                partialFlowCts.Dispose();
                partialStream?.Dispose();
                throw;
            }

            return partialStream!;
        }

        public async UniTask<IWebRequest> SendAsync(ITypedWebRequest requestWrap, CancellationToken ct)
        {
            await using ExecuteOnMainThreadScope scope = await ExecuteOnMainThreadScope.NewScopeWithReturnOnOriginalThreadAsync();

            RequestEnvelope envelope = requestWrap.Envelope;

            using ITypedWebRequest _ = requestWrap;
            HTTPRequest nativeRequest = requestWrap.CreateHttp2Request();

            var requestAdapter = new Http2WebRequest(nativeRequest);

            envelope.InitializedWebRequest(identityCache, requestAdapter);
            nativeRequest.RetrySettings.MaxRetries = envelope.CommonArguments.TotalAttempts();
            nativeRequest.DownloadSettings.ContentStreamMaxBuffered = requestWrap.DownloadBufferMaxSize;

            envelope.OnCreated?.Invoke(requestAdapter);

            try { await ExecuteWithAnalytics(requestWrap, requestAdapter, envelope.ReportData, ct); }
            catch (AsyncHTTPException exception)
            {
                if (!envelope.SuppressErrors)

                    // Print verbose
                    ReportHub.LogError(
                        envelope.ReportData,
                        $"Exception occured on loading {requestWrap.GetType().Name} from {envelope.CommonArguments.URL} with args {requestWrap.ArgsToString()},\n with {envelope}\n"
                    );

                // Dispose adapter on exception as it won't be returned to the caller
                requestAdapter.Dispose();

                // convert into a common exception
                throw new Http2WebRequestException(requestAdapter, exception);
            }

            return requestAdapter;
        }

        private async UniTask ExecuteWithAnalytics(ITypedWebRequest request, Http2WebRequest adapter, ReportData reportData, CancellationToken ct)
        {
            analyticsContainer.OnRequestStarted(request, adapter);

            HTTPRequest nativeRequest = adapter.httpRequest;

            try
            {
                UniTask<HTTPResponse> coreTask = nativeRequest.GetHTTPResponseAsync(ct);
                var checkBufferCt = coreTask.ToCancellationToken();

                if (request.StreamingSupported)
                    await coreTask;
                else
                    await UniTask.WhenAny(
                        coreTask,
                        CheckBufferIsFull(nativeRequest, request, reportData, checkBufferCt));
            }
            finally { analyticsContainer.OnRequestFinished(request, adapter); }

            static async UniTask CheckBufferIsFull(HTTPRequest nativeRequest, ITypedWebRequest request, ReportData reportData, CancellationToken ct)
            {
                // ReSharper disable once AccessToModifiedClosure
                while (!ct.IsCancellationRequested)
                {
                    if (nativeRequest.Response is { DownStream: { IsFull: true } })
                    {
                        ReportHub.LogError(reportData, $"Download Data exceeded {((ulong)request.DownloadBufferMaxSize).ByteToMB()}MB on loading {request.GetType().Name} from {nativeRequest.Uri} with args {request.ArgsToString()}");
                        nativeRequest.Abort();
                        break;
                    }

                    await UniTask.Yield(ct);
                }
            }
        }
    }
}
