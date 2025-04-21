using Best.HTTP;
using Best.HTTP.Caching;
using Best.HTTP.Request.Settings;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.RequestsHub;
using System;
using System.Threading;

namespace DCL.WebRequests.HTTP2
{
    public class Http2WebRequestController : IWebRequestController
    {
        private readonly IWeb3IdentityCache identityCache;
        private readonly IWebRequestsAnalyticsContainer analyticsContainer;
        private readonly IRequestHub requestHub;
        private readonly HTTPCache cache;
        private readonly long chunkSize;

        private readonly OnDownloadStartedDelegate detachDownloadStream;

        IRequestHub IWebRequestController.requestHub => requestHub;

        public Http2WebRequestController(IWebRequestsAnalyticsContainer analyticsContainer, IWeb3IdentityCache identityCache, IRequestHub requestHub, HTTPCache cache, long chunkSize)
        {
            this.identityCache = identityCache;
            this.analyticsContainer = analyticsContainer;
            this.requestHub = requestHub;
            this.cache = cache;
            this.chunkSize = chunkSize;

            // We can't await for DownloadStream straight-away as there is a chance that the request will be finished and disposed before we actually hit the valid stream
            // so it will be disposed and stay `null`
            detachDownloadStream = (_, _, stream) => stream.IsDetached = true;
        }

        public UniTask<PartialDownloadStream> GetPartialAsync(CommonArguments commonArguments, ReportData reportData, PartialDownloadArguments partialArgs, CancellationToken ct, WebRequestHeadersInfo? headersInfo = null)
        {
            // If the result is fully cached in the stream that was passed, return it immediately
            var partialStream = partialArgs.Stream as Http2PartialDownloadDataStream;

            var uri = new Uri(commonArguments.URL);

            if (partialStream is { IsFullyDownloaded: true })
            {
                ReportHub.Log(ReportCategory.PARTIAL_LOADING, $"{nameof(PartialDownloadStream)} {commonArguments.URL} is already fully downloaded");
                return UniTask.FromResult<PartialDownloadStream>(partialStream);
            }

            if (uri.IsFile)
            {
                try { return UniTask.FromResult<PartialDownloadStream>(Http2PartialDownloadDataStream.InitializeFromFile(uri)); }
                catch (Exception ex) { throw new WebRequestException(uri, ex); }
            }

            // if the result is already fully cached return it immediately without creating a web request
            if (partialStream == null
                && Http2PartialDownloadDataStream.TryInitializeFromCache(cache, uri, HTTPCache.CalculateHash(HTTPMethods.Get, uri), chunkSize, out partialStream)
                && partialStream!.IsFullyDownloaded)
                return UniTask.FromResult<PartialDownloadStream>(partialStream);


            // Create headers accordingly, at this point don't create a partial stream as we don't know if the endpoint actually supports "Range" requests
            long chunkStart = partialStream?.partialContentLength ?? 0;
            long chunkEnd = partialStream == null ? chunkSize : Math.Min(partialStream.fullFileSize - 1, chunkStart + chunkSize);

            headersInfo = (headersInfo ?? new WebRequestHeadersInfo()).WithRange(chunkStart, chunkEnd);

            // Recreate arguments as the partial stream could be initialized from cache
            return
                this.Create<PartialDownloadRequest, PartialDownloadArguments>(new PartialDownloadArguments(partialStream), commonArguments, reportData, headersInfo)
                    .PartialFlowAsync(ct);
        }

        public async UniTask<IWebRequest> SendAsync(ITypedWebRequest requestWrap, bool detachDownloadHandler, CancellationToken ct)
        {
            bool fromMainThread = PlayerLoopHelper.IsMainThread;

            if (fromMainThread)
                await UniTask.SwitchToThreadPool();

            // Doesn't work with the scope, not clear why
            // await using ExecuteOnThreadPoolScope scope = await ExecuteOnThreadPoolScope.NewScopeWithReturnOnOriginalThreadAsync();

            // Don't Dispose the wrap here as it can outlive the original request to process the response
            RequestEnvelope envelope = requestWrap.Envelope;

            Http2WebRequest? requestAdapter = null;

            try
            {
                HTTPRequest nativeRequest = requestWrap.CreateHttp2Request();

                requestAdapter = new Http2WebRequest(nativeRequest, requestWrap);

                envelope.InitializedWebRequest(identityCache, requestAdapter);
                nativeRequest.RetrySettings.MaxRetries = envelope.CommonArguments.TotalAttempts();
                nativeRequest.DownloadSettings.ContentStreamMaxBuffered = requestWrap.DownloadBufferMaxSize;

                if (detachDownloadHandler)
                    nativeRequest.DownloadSettings.OnDownloadStarted += detachDownloadStream;

                envelope.OnCreated?.Invoke(requestAdapter);

                await ExecuteWithAnalyticsAsync(requestWrap, requestAdapter, envelope.ReportData, ct);

                requestAdapter.successfullyExecutedByController = true;
                return requestAdapter;
            }
            catch (AsyncHTTPException exception)
            {
                if (!envelope.SuppressErrors)

                    // Print verbose
                    ReportHub.LogError(
                        envelope.ReportData,
                        $"Exception occured on loading {requestWrap.GetType().Name} from {envelope.CommonArguments.URL} with args {requestWrap.ArgsToString()},\n with {envelope}\n{exception}"
                    );

                var adaptedException = new Http2WebRequestException(requestAdapter!, exception);

                // Dispose adapter on exception as it won't be returned to the caller
                requestAdapter!.Dispose();

                // convert into a common exception
                throw adaptedException;
            }
            catch // any other exception
            {
                // Dispose adapter on exception as it won't be returned to the caller
                requestAdapter?.Dispose();

                throw;
            }
            finally
            {
                if (fromMainThread)
                    await UniTask.SwitchToMainThread();
            }
        }

        private async UniTask ExecuteWithAnalyticsAsync(ITypedWebRequest request, Http2WebRequest adapter, ReportData reportData, CancellationToken ct)
        {
            analyticsContainer.OnRequestStarted(request, adapter);

            HTTPRequest nativeRequest = adapter.httpRequest;

            try
            {
                ct.ThrowIfCancellationRequested();

                // TODO there is somewhere a bug with cancellation on application exit that makes ITypedRequest leak
                UniTask<HTTPResponse> coreTask = nativeRequest.GetHTTPResponseAsync(ct);

                if (request.StreamingSupported)
                    await coreTask;
                else
                {
                    var checkBufferCts = new CancellationTokenSource();
                    CheckBufferIsFullAsync(nativeRequest, request, reportData, checkBufferCts.Token).Forget();

                    try { await coreTask; }
                    finally
                    {
                        checkBufferCts.Cancel();
                        checkBufferCts.Dispose();
                    }
                }

            }
            finally { analyticsContainer.OnRequestFinished(request, adapter); }

            static async UniTask CheckBufferIsFullAsync(HTTPRequest nativeRequest, ITypedWebRequest request, ReportData reportData, CancellationToken ct)
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
