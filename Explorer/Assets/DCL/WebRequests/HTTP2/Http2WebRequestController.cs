using Best.HTTP;
using Best.HTTP.Caching;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.RequestsHub;
using System;
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

            // Recreate arguments as the partial stream could be initialized from cache
            return await
                this.Create<PartialDownloadRequest, PartialDownloadArguments>(new PartialDownloadArguments(partialStream), commonArguments, ReportCategory.PARTIAL_LOADING, headersInfo)
                    .PartialFlowAsync(ct);
        }

        public async UniTask<IWebRequest> SendAsync(ITypedWebRequest requestWrap, CancellationToken ct)
        {
            await using ExecuteOnThreadPoolScope scope = await ExecuteOnThreadPoolScope.NewScopeWithReturnOnOriginalThreadAsync();

            RequestEnvelope envelope = requestWrap.Envelope;

            // Don't Dispose the wrap here as it can outlive the original request to process the response
            HTTPRequest nativeRequest = requestWrap.CreateHttp2Request();

            var requestAdapter = new Http2WebRequest(nativeRequest, requestWrap);

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
