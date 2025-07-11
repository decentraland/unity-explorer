using Best.HTTP;
using Best.HTTP.Caching;
using Best.HTTP.Shared.Logger;
using Best.HTTP.Shared.PlatformSupport.Memory;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests.HTTP2;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using Utility.Multithreading;

namespace DCL.WebRequests
{
    /// <summary>
    ///     Serves as a storage of data received from <see cref="HTTPRequest" />'s callbacks to avoid massive closures
    /// </summary>
    public class PartialDownloadRequest : TypedWebRequestBase<PartialDownloadArguments>
    {
        private readonly bool partialDownloadingIsEnabled;
        private readonly WebRequestsMode mode;

        private readonly HTTPCache cache;
        private readonly long chunkSize;
        private readonly byte maxChunksCount;

        private volatile HTTPRequest? request;
        private volatile Dictionary<string, List<string>>? storedHeaders;
        private Http2PartialDownloadDataStream? partialStream;

        private CancellationTokenSource? partialFlowCts;

        public override bool Http2Supported => true;
        public override bool StreamingSupported => true;

        internal PartialDownloadRequest(HTTPCache cache,
            RequestEnvelope envelope,
            PartialDownloadArguments args,
            IWebRequestController controller,
            long chunkSize,
            byte maxChunksCount,
            bool partialDownloadingIsEnabled,
            WebRequestsMode mode)
            : base(envelope, args, controller)
        {
            this.cache = cache;
            this.partialDownloadingIsEnabled = partialDownloadingIsEnabled;
            this.mode = mode;
            this.chunkSize = chunkSize;
            this.maxChunksCount = maxChunksCount;
            partialStream = args.Stream as Http2PartialDownloadDataStream;
        }

        public override HTTPRequest CreateHttp2Request()
        {
            request = new HTTPRequest(commonArguments.URL, HTTPMethods.Get);

            // Default Cache should be disabled as Http2PartialDownloadDataStream caches both partial and non-partial requests
            request.DownloadSettings.DisableCache = true;

            // Warning: events are delayed, a data copy is passed to events (such as headers, may allocate heavily)
            // But response.Headers are not threadsafe =-( so we have to use a copy to avoid random `null` errors
            request.DownloadSettings.OnHeadersReceived += OnHeadersReceived;

            return request;
        }

        public UniTask<PartialDownloadStream> GetStreamAsync(CancellationToken ct)
        {
            if (!partialDownloadingIsEnabled)
                throw new NotSupportedException("Partial Downloading is disabled");

            return mode switch
                   {
                       WebRequestsMode.YET_ANOTHER => GetStreamFromYetAnotherRequestAsync(ct),
                       _ => throw new NotSupportedException($"WebRequestsMode {mode} is not supported for {nameof(PartialDownloadRequest)}"),
                   };
        }

        public override (HttpRequestMessage, ulong uploadPayloadSize) CreateYetAnotherHttpRequest() =>
            new (new HttpRequestMessage(HttpMethod.Get, commonArguments.URL), 0);

        private async UniTask<PartialDownloadStream> GetStreamFromYetAnotherRequestAsync(CancellationToken ct)
        {
            if (PlayerLoopHelper.IsMainThread)
                await UniTask.SwitchToThreadPool();

            Uri uri = commonArguments.URL;

            if (partialStream is { IsFullyDownloaded: true })
            {
                ReportHub.Log(ReportCategory.PARTIAL_LOADING, $"{nameof(PartialDownloadStream)} {commonArguments.URL} is already fully downloaded");
                return DisposeAndReturn();
            }

            // BestHTTP does not use the file stream directly, instead it allocates chunks via SegmentBuffer as with a regular request
            // We fix it here
            if (uri.IsFile)
            {
                try
                {
                    partialStream = Http2PartialDownloadDataStream.InitializeFromFile(uri);
                    return DisposeAndReturn();
                }
                catch (Exception ex)
                {
                    Dispose();
                    throw new WebRequestException(uri, ex);
                }
            }

            // if the result is already fully cached return it immediately without creating a web request
            if (partialStream == null
                && Http2PartialDownloadDataStream.TryInitializeFromCache(cache, uri, HTTPCache.CalculateHash(HTTPMethods.Get, uri), chunkSize, maxChunksCount, out partialStream)
                && partialStream!.IsFullyDownloaded)
                return DisposeAndReturn();

            // Otherwise a new request is needed
            // Create headers accordingly, at this point don't create a partial stream as we don't know if the endpoint actually supports "Range" requests

            long effectiveChunkSize = partialStream?.effectiveChunkSize ?? chunkSize;

            long chunkStart = partialStream?.partialContentLength ?? 0;
            long chunkEnd = partialStream == null ? effectiveChunkSize - 1 : Math.Min(partialStream.fullFileSize - 1, chunkStart + effectiveChunkSize - 1);

            envelope = new RequestEnvelope(envelope.CommonArguments, envelope.ReportData,
                envelope.HeadersInfo.WithRange(chunkStart, chunkEnd), envelope.SignInfo, envelope.SuppressErrors, envelope.OnCreated);

            var createdRequest = (YetAnotherWebRequest)await this.SendAsync(ct); // It will return the control when the headers are received

            // Now we just need to linearly read

            HttpRequestMessage nativeRequest = createdRequest.request;
            YetAnotherWebResponse nativeResponse = createdRequest.response!;

            var headers = new WebRequestHeaders(nativeResponse.response);

            Http2PartialDownloadDataStream? result;

            try
            {
                HTTPMethods method = Enum.Parse<HTTPMethods>(nativeRequest.Method.Method, true);
                int statusCode = nativeResponse.StatusCode;

                MultithreadingUtility.AssertMainThread(nameof(Http2PartialDownloadDataStream.TryInitializeFromHeaders));

                // Check headers
                // If at any stage headers are not correct discard the partial stream and the cached result, as the server does not support any headers
                if (!Http2PartialDownloadDataStream.TryInitializeFromHeaders(cache, uri, method, statusCode, null, headers, ref partialStream, chunkSize, maxChunksCount, out long expectedChunkLength))
                {
                    if (partialStream == null && nativeResponse.response.Content.Headers.ContentRange == null) // We can continue only if it's not a partial request
                        partialStream = Http2PartialDownloadDataStream.InitializeFromUnknownSource(uri);
                    else
                    {
                        throw CreateException($"Failed to initialize {nameof(Http2PartialDownloadDataStream)} from headers for {uri}\n"
                                              + $"Expected Range: {chunkStart}-{chunkEnd}");
                    }
                }

                long startPartialLength = partialStream!.partialContentLength;

                YetAnotherDownloadContentStream downStream = nativeResponse.downStream;
                LoggingContext? loggingContext = null;

                // Keep non-blocking reading from the download stream

                while (true)
                {
                    (BufferSegment segment, bool finished) = await downStream.TryTakeNextAsync(ct);

                    if (segment.Count > 0)
                        if (!partialStream!.TryAppend(uri, segment, loggingContext))
                            throw CreateException($"{nameof(Http2PartialDownloadDataStream.TryAppend)} failed");

                    if (finished)
                        break;
                }

                ct.ThrowIfCancellationRequested();

                // Check that enough data was downloaded
                if (expectedChunkLength != 0)
                {
                    var downloadLength = (int)(partialStream!.partialContentLength - startPartialLength);

                    if (downloadLength != expectedChunkLength)
                        throw CreateException($"Expected to load {expectedChunkLength} bytes, but loaded {downloadLength} bytes");
                }
                else
                {
                    // Finalize the downloading as we don't know the expected chunk size
                    partialStream.ForceFinalize();
                }
            }
            catch (Exception)
            {
                // Dispose it here as well to break ContentReceiveLoop if needed
                partialStream?.DiscardAndDispose();
                throw;
            }
            finally
            {
                result = partialStream!;
                createdRequest.Dispose();
            }

            return result;
        }

        private PartialDownloadStream DisposeAndReturn()
        {
            Http2PartialDownloadDataStream? stream = partialStream;
            Dispose();
            return stream!;
        }

        private void OnHeadersReceived(HTTPRequest req, HTTPResponse resp, Dictionary<string, List<string>> headers)
        {
            // We don't process it here to delegate it to the thread pool
            storedHeaders = headers;
        }

        private Exception CreateException(string message) =>
            new ($"Exception occured in {nameof(GetStreamFromYetAnotherRequestAsync)} {Envelope.CommonArguments.URL}: {message}");

        protected override void OnDispose()
        {
            request = null;
            storedHeaders = null;
            partialStream = null;
        }
    }
}
