using Best.HTTP;
using Best.HTTP.Caching;
using Best.HTTP.Response;
using Best.HTTP.Shared.Logger;
using Best.HTTP.Shared.PlatformSupport.Memory;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests.HTTP2;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Utility;
using Utility.Multithreading;

namespace DCL.WebRequests
{
    /// <summary>
    ///     Serves as a storage of data received from <see cref="HTTPRequest" />'s callbacks to avoid massive closures
    /// </summary>
    public class PartialDownloadRequest : TypedWebRequestBase<PartialDownloadArguments>
    {
        private readonly bool partialDownloadingIsEnabled;

        private readonly HTTPCache cache;
        private readonly long chunkSize;

        private volatile HTTPRequest? request;
        private volatile Dictionary<string, List<string>>? storedHeaders;
        private volatile Http2PartialDownloadDataStream? partialStream;

        private CancellationTokenSource? partialFlowCts;

        public override bool Http2Supported => true;
        public override bool StreamingSupported => true;

        internal PartialDownloadRequest(HTTPCache cache,
            RequestEnvelope envelope,
            PartialDownloadArguments args,
            IWebRequestController controller,
            long chunkSize,
            bool partialDownloadingIsEnabled)
            : base(envelope, args, controller)
        {
            this.cache = cache;
            this.partialDownloadingIsEnabled = partialDownloadingIsEnabled;
            this.chunkSize = chunkSize;
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

        public async UniTask<PartialDownloadStream> GetStreamAsync(CancellationToken ct)
        {
            if (!partialDownloadingIsEnabled)
                throw new NotSupportedException("Partial Downloading is disabled");

            // If the result is fully cached in the stream that was passed, return it immediately

            var uri = new Uri(commonArguments.URL);

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
                && Http2PartialDownloadDataStream.TryInitializeFromCache(cache, uri, HTTPCache.CalculateHash(HTTPMethods.Get, uri), chunkSize, out partialStream)
                && partialStream!.IsFullyDownloaded)
                return DisposeAndReturn();

            PartialDownloadStream DisposeAndReturn()
            {
                Http2PartialDownloadDataStream? stream = partialStream;
                Dispose();
                return stream!;
            }

            // Otherwise a new request is needed
            // Create headers accordingly, at this point don't create a partial stream as we don't know if the endpoint actually supports "Range" requests
            long chunkStart = partialStream?.partialContentLength ?? 0;
            long chunkEnd = partialStream == null ? chunkSize - 1 : Math.Min(partialStream.fullFileSize - 1, chunkStart + chunkSize - 1);

            envelope = new RequestEnvelope(envelope.CommonArguments, envelope.ReportData,
                envelope.HeadersInfo.WithRange(chunkStart, chunkEnd), envelope.SignInfo, envelope.SuppressErrors, envelope.OnCreated);

            IWebRequest? createdRequest = null;

            partialFlowCts = new CancellationTokenSource();
            PartialDownloadStream result;

            async UniTask WaitForRequestAsync()
            {
                try { createdRequest = await this.SendAsync(ct); }
                catch (Exception)
                {
                    // If an exception in the core flow occurs cancel the partial flow
                    partialFlowCts.SafeCancelAndDispose();
                    throw;
                }
            }

            try
            {
                ct.ThrowIfCancellationRequested();

                await UniTask.WhenAll(
                    WaitForRequestAsync(),
                    ProcessPartialDownloadStreamAsync(partialFlowCts.Token)); // Throws Task Canceled Exception
            }
            finally
            {
                // Assign the stream before disposal
                result = partialStream!;
                createdRequest?.Dispose();
            }

            return result;
        }

        private async UniTask ProcessPartialDownloadStreamAsync(CancellationToken ct)
        {
            await UniTask.SwitchToThreadPool();

            try
            {
                // Wait for the request to be created
                while (request == null)
                    await PollDelayAsync(ct);

                // Wait for headers
                while (storedHeaders == null)
                    await PollDelayAsync(ct);

                // Check headers
                // If at any stage headers are not correct discard the partial stream and the cached result, as the server does not support any headers
                if (!Http2PartialDownloadDataStream.TryInitializeFromHeaders(cache, request, storedHeaders, ref partialStream, out long expectedChunkLength))
                {
                    partialStream?.DiscardAndDispose();
                    partialStream = Http2PartialDownloadDataStream.InitializeFromUnknownSource(request.Uri);
                }

                long startPartialLength = partialStream!.partialContentLength;

                // Wait for the download stream to be created
                while (request.Response is not { DownStream: not null })
                    await PollDelayAsync(ct);

                using DownloadContentStream? downStream = request.Response.DownStream;
                LoggingContext? loggingContext = request.Response.Context;

                const int CONTENT_POLL_INTERVAL = 3;

                // Keep non-blocking reading from the download stream
                do
                {
                    ct.ThrowIfCancellationRequested();

                    if (downStream.TryTake(out BufferSegment segment))
                        if (!partialStream!.TryAppend(request.Uri, segment, loggingContext))
                            throw CreateException($"{nameof(Http2PartialDownloadDataStream.TryAppend)} failed");

                    Thread.Sleep(CONTENT_POLL_INTERVAL);
                }
                while (!downStream.IsCompleted);

                if (!request.Response.IsSuccess)
                {
                    const string FAIL_ERROR = "Download Stream has completed but the request has finished unsuccessfully. "
                                              + "The Partial Stream will be disposed of to prevent data corruption";

                    ReportHub.LogWarning(ReportCategory.PARTIAL_LOADING, $"{request.Uri} {FAIL_ERROR}");
                    throw new Exception(FAIL_ERROR);
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
            catch (Exception e)
            {
                // Dispose it here as well to break ContentReceiveLoop if needed
                // partialFlowCts.SafeCancelAndDispose();
                partialStream?.DiscardAndDispose();

                bool isLoopException = request?.State < HTTPRequestStates.Finished;
                LogType logType = isLoopException ? LogType.Error : LogType.Warning;

                // If the request is not finished, this exception is the own exception of the content loop, otherwise it's delays due to concurrency
                if (isLoopException)
                {
                    ReportHub.Log(logType, ReportCategory.PARTIAL_LOADING, $"Content Receive Loop of {request?.Uri} was broken due to {e}");
                    request?.Abort();
                }

                if (isLoopException)
                    request?.Abort();

                // If it is a cancellation, it is the result of the parent task so it should not be propagated further
                if (isLoopException && e is not TaskCanceledException and OperationCanceledException)
                    throw;
            }
        }

        // Can't use UniTask.Delay as it will switch to the main thread
        private static Task PollDelayAsync(CancellationToken ct) =>
            Task.Delay(10, ct);

        private void OnHeadersReceived(HTTPRequest req, HTTPResponse resp, Dictionary<string, List<string>> headers)
        {
            // We don't process it here to delegate it to the thread pool
            storedHeaders = headers;
        }

        private Exception CreateException(string message) =>
            new ($"Exception occured in {nameof(GetStreamAsync)} {Envelope.CommonArguments.URL}: {message}");

        protected override void OnDispose()
        {
            request = null;
            storedHeaders = null;
            partialStream = null;
        }
    }
}
