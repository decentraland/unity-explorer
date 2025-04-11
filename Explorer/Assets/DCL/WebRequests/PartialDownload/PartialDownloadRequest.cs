using Best.HTTP;
using Best.HTTP.Caching;
using Best.HTTP.Response;
using Best.HTTP.Shared.Logger;
using Best.HTTP.Shared.PlatformSupport.Memory;
using Cysharp.Threading.Tasks;
using DCL.WebRequests.HTTP2;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Utility;
using Utility.Multithreading;
using Utility.Types;

namespace DCL.WebRequests
{
    /// <summary>
    ///     Serves as a storage of data received from <see cref="HTTPRequest" />'s callbacks to avoid massive closures
    /// </summary>
    public class PartialDownloadRequest : TypedWebRequestBase<PartialDownloadArguments>
    {
        private readonly HTTPCache cache;

        private volatile HTTPRequest? request;
        private volatile Dictionary<string, List<string>>? storedHeaders;
        private volatile Http2PartialDownloadDataStream? partialStream;
        private CancellationTokenSource partialFlowCts;

        public override bool Http2Supported => true;
        public override bool StreamingSupported => true;

        internal PartialDownloadRequest(HTTPCache cache, RequestEnvelope envelope, PartialDownloadArguments args, IWebRequestController controller) : base(envelope, args, controller)
        {
            this.cache = cache;
            partialStream = args.Stream as Http2PartialDownloadDataStream;
        }

        public override HTTPRequest CreateHttp2Request()
        {
            request = new HTTPRequest(Envelope.CommonArguments.URL, HTTPMethods.Get);

            // Default Cache should be disabled as Http2PartialDownloadDataStream both partial and non-partial requests
            request.DownloadSettings.DisableCache = true;

            // Warning: events are delayed, a data copy is passed to events (such as headers, may allocate heavily)
            // But response.Headers are not threadsafe =-( so we have to use a copy to avoid random `null` errors
            request.DownloadSettings.OnHeadersReceived += OnHeadersReceived;

            // We can't await for DownloadStream straight-away as there is a chance that the request will be finished and disposed before we actually hit the valid stream
            // so it will be disposed and stay `null`
            request.DownloadSettings.OnDownloadStarted += OnDownloadStarted;

            return request;
        }

        public async UniTask<PartialDownloadStream> PartialFlowAsync(CancellationToken ct)
        {
            await ExecuteOnThreadPoolScope.NewScopeWithReturnOnOriginalThreadAsync();

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
                    ProcessPartialDownloadStreamAsync(partialFlowCts.Token)); // Throws Task Cancellated Exception
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
                if (!Http2PartialDownloadDataStream.TryInitializeFromHeaders(cache, request, storedHeaders, ref partialStream, out int expectedChunkLength))
                {
                    partialStream?.DiscardAndDispose();
                    partialStream = Http2PartialDownloadDataStream.InitializeFromUnknownSource();
                }

                long startPartialLength = partialStream!.partialContentLength;

                // Wait for the download stream to be created
                while (request.Response is not { DownStream: not null })
                    await PollDelayAsync(ct);

                DownloadContentStream? downStream = request.Response.DownStream;
                LoggingContext? loggingContext = request.Response.Context;

                // Keep non-blocking reading from the download stream
                do
                {
                    ct.ThrowIfCancellationRequested();

                    if (downStream.TryTake(out BufferSegment segment))
                        if (!partialStream!.TryAppend(segment, loggingContext))
                            throw CreateException($"{nameof(Http2PartialDownloadDataStream.TryAppend)} failed");

                    await PollDelayAsync(ct);
                }
                while (!downStream.IsCompleted);

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
            catch (TaskCanceledException)
            {
                // If it is a cancellation it is the result of the parent task so it should not be propagated further
                request?.Response?.DownStream?.Dispose();
            }
            catch (Exception)
            {
                // Dispose it here as well to break ContentReceiveLoop if needed
                partialFlowCts.SafeCancelAndDispose();
                partialStream?.DiscardAndDispose();
                request?.Response?.DownStream?.Dispose();
                throw;
            }
        }

        // Can't use UniTask.Delay as it will switch to the main thread
        private static Task PollDelayAsync(CancellationToken ct) =>
            Task.Delay(10, ct);

        private void OnDownloadStarted(HTTPRequest req, HTTPResponse resp, DownloadContentStream stream)
        {
            stream.IsDetached = true; // Detach straight-away to avoid preliminary Disposal
        }

        private void OnHeadersReceived(HTTPRequest req, HTTPResponse resp, Dictionary<string, List<string>> headers)
        {
            // We don't process it here to delegate it to the thread pool
            storedHeaders = headers;
        }

        private Exception CreateException(string message) =>
            new ($"Exception occured in {nameof(PartialFlowAsync)} {Envelope.CommonArguments.URL}: {message}");

        protected override void OnDispose()
        {
            request = null;
            storedHeaders = null;
            partialStream = null;
        }
    }
}
