using Best.HTTP;
using Best.HTTP.Caching;
using Best.HTTP.Response;
using Best.HTTP.Shared;
using Best.HTTP.Shared.Extensions;
using Best.HTTP.Shared.Logger;
using Best.HTTP.Shared.PlatformSupport.FileSystem;
using Best.HTTP.Shared.PlatformSupport.Memory;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.Optimization.ThreadSafePool;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Pool;
using static DCL.WebRequests.WebRequestHeaders;

namespace DCL.WebRequests.HTTP2
{
    /// <summary>
    ///     <inheritdoc cref="PartialDownloadStream" />
    ///     <br />
    ///     Supports both requests with "Range" headers and without them in the same way
    /// </summary>
    public class Http2PartialDownloadDataStream : PartialDownloadStream
    {
        public enum Mode
        {
            UNITIALIZED = 0,

            /// <summary>
            ///     Incomplete data is loaded from cache, but it's not available to be read from outside
            /// </summary>
            INCOMPLETE_DATA_CACHED = 1,

            /// <summary>
            ///     Space for cache is reserved and the file is open for write
            /// </summary>
            WRITING_TO_DISK_CACHE = 2,

            /// <summary>
            ///     The whole file is cached and available to be read, writing is no longer possible
            /// </summary>
            COMPLETE_DATA_CACHED = 3,

            /// <summary>
            ///     The file is server from memory by <see cref="BufferSegment" />s
            /// </summary>
            WRITING_TO_SEGMENTED_STREAM = 4,

            /// <summary>
            ///     The whole file is cached in memory and available to be read, writing is no longer possible
            /// </summary>
            COMPLETE_SEGMENTED_STREAM = 5,

            /// <summary>
            ///     File is provided directly from the file system and thus it's never partial,
            ///     BESTHttp does not provide a file stream directly so it loads it into the memory chunks
            /// </summary>
            EMBEDDED_FILE_STREAM = 6,
        }

        internal const string PARTIAL_CONTENT_LENGTH_CUSTOM_HEADER = "Partial-Content-Length";
        internal const string PARTIAL_CONTENT_FULL_LENGTH_CUSTOM_HEADER = "Partial-Content-Full-Length";

        private static readonly string[] CACHE_CONTROL_HEADERS =
        {
            CONTENT_LENGTH_HEADER,
            "cache-control",
            "etag",
            "expires",
            "last-modified",
            "age",
            "date",
        };

        private static readonly ProfilerMarker DISPOSE_MARKER = new ($"{nameof(Http2PartialDownloadDataStream)}/{nameof(Dispose)}");
        private static readonly ProfilerMarker TRY_INITIALIZE_FROM_HEADERS_MARKER = new ($"{nameof(Http2PartialDownloadDataStream)}/{nameof(TryInitializeFromHeaders)}");
        private static readonly ProfilerMarker TRY_APPEND_MARKER = new ($"{nameof(Http2PartialDownloadDataStream)}/{nameof(TryAppend)}");

        private static readonly ThreadSafeListPool<string> REDUNDANT_HEADERS_POOL =
            new (15, 50);

        internal readonly long fullFileSize;

        /// <summary>
        ///     Breaking the big file into many small chunks lead to the worse downloading time due to creation of many requests and throttling between them
        /// </summary>
        internal readonly long effectiveChunkSize;

        private readonly Uri fromUrl;

        private CachedPartialData cachedPartialData;
        private MemoryStreamPartialData memoryStreamPartialData;
        private FileStreamData fileStreamData;

        private bool discardOnDisposal;

        public override bool IsFullyDownloaded => OpMode is Mode.COMPLETE_DATA_CACHED or Mode.COMPLETE_SEGMENTED_STREAM or Mode.EMBEDDED_FILE_STREAM;

        public Mode OpMode { get; private set; }

        internal long partialContentLength => OpMode switch
                                              {
                                                  Mode.COMPLETE_DATA_CACHED or Mode.INCOMPLETE_DATA_CACHED or Mode.WRITING_TO_DISK_CACHE => cachedPartialData.partialContentLength,
                                                  Mode.COMPLETE_SEGMENTED_STREAM or Mode.WRITING_TO_SEGMENTED_STREAM => memoryStreamPartialData.stream.Length,
                                                  _ => 0,
                                              };

        private Http2PartialDownloadDataStream(Uri fromUrl, long fullFileSize, long effectiveChunkSize)
        {
            this.fromUrl = fromUrl;
            this.fullFileSize = fullFileSize;
            this.effectiveChunkSize = effectiveChunkSize;
        }

        internal ref readonly CachedPartialData GetCachedPartialData() =>
            ref cachedPartialData;

        internal ref readonly MemoryStreamPartialData GetMemoryStreamPartialData() =>
            ref memoryStreamPartialData;

        internal ref readonly FileStreamData GetFileStreamData() =>
            ref fileStreamData;

        /// <summary>
        ///     Allows to read directly from the file stream without going through the normal flow of the web requests
        /// </summary>
        internal static Http2PartialDownloadDataStream InitializeFromFile(Uri uri)
        {
            string filePath = uri.LocalPath;
            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            ReportHub.Log(ReportCategory.PARTIAL_LOADING, $"Partial Download Stream {uri} initialized from the embedded file");

            return new Http2PartialDownloadDataStream(uri, (int)fileStream.Length, 0)
            {
                fileStreamData = new FileStreamData(fileStream),
                OpMode = Mode.EMBEDDED_FILE_STREAM,
            };
        }

        internal static bool TryInitializeFromCache(HTTPCache cache, Uri uri, Hash128 requestHash, long chunkSizeAlignment, byte maxChunksCount,
            out Http2PartialDownloadDataStream? partialStream)
        {
            partialStream = null;

            // Check if it is cached
            if (!cache.AreCacheFilesExists(requestHash))
                return false;

            // Start reading from the cache acquiring a lock so the entry won't be deleted on maintenance
            // Thus, we increase the read lock so we need to release it later
            // The locks works for both Headers and Content
            Stream? stream = cache.BeginReadContent(requestHash, null);

            if (stream == null)
                return false;

            if (!TryReadHeaders(cache, requestHash, out WebRequestHeaders headers))
            {
                EmergencyEndCacheRead();
                return false;
            }

            if (!TryParseCachedContentSize(headers.value, out long cachedSize, out long fullFileSize))
            {
                headers.Dispose();

                EmergencyEndCacheRead();
                return false;
            }

            // The cached size must be aligned with the Chunk Size (and the chunk size must be aligned with 1MB/2MB - this is how CloudFront works
            // It will not serve arbitrary/random ranges (will result in 416 http error)
            if ((cachedSize < fullFileSize && cachedSize % chunkSizeAlignment != 0) || cachedSize > fullFileSize)
            {
                ReportHub.Log(ReportCategory.PARTIAL_LOADING, $"Cached data of {uri} ({requestHash}) {((ulong)cachedSize).ByteToMB()}MB is not aligned with the chunk size {((ulong)chunkSizeAlignment).ByteToMB()}MB and will be invalidated");

                EmergencyEndCacheRead();

                // Remove the cached chunk to reset the progress
                cache.Delete(requestHash, false, null);
                return false;
            }

            void EmergencyEndCacheRead()
            {
                stream.Dispose();
                cache.EndReadContent(requestHash, null);
            }

            partialStream = new Http2PartialDownloadDataStream(uri, fullFileSize, CalculateEffectiveChunkSize(chunkSizeAlignment, fullFileSize, maxChunksCount));

            partialStream.cachedPartialData = new CachedPartialData(headers, cache, requestHash)
            {
                partialContentLength = cachedSize,
                readHandler = new CacheReadHandler(stream),
            };

            // Set the state - whether it is fully cached or not
            partialStream.OpMode = partialStream.cachedPartialData.partialContentLength == fullFileSize
                ? Mode.COMPLETE_DATA_CACHED
                : Mode.INCOMPLETE_DATA_CACHED;

            ReportHub.Log(ReportCategory.PARTIAL_LOADING, $"Partial Download Stream {uri} ({requestHash}) initialized as {partialStream.OpMode} "
                                                          + $"{BytesFormatter.Convert((ulong)partialStream.cachedPartialData.partialContentLength, BytesFormatter.DataSizeUnit.Byte, BytesFormatter.DataSizeUnit.Kilobyte)} / "
                                                          + $"{BytesFormatter.Convert((ulong)fullFileSize, BytesFormatter.DataSizeUnit.Byte, BytesFormatter.DataSizeUnit.Kilobyte)} KB");

            return true;

            static bool TryParseCachedContentSize(Dictionary<string, List<string>> headers, out long cachedSize, out long fullSize)
            {
                fullSize = 0;

                return Http2Utils.TryParseHeaderLong(headers, PARTIAL_CONTENT_LENGTH_CUSTOM_HEADER, ReportCategory.PARTIAL_LOADING, out cachedSize) &&
                       Http2Utils.TryParseHeaderLong(headers, PARTIAL_CONTENT_FULL_LENGTH_CUSTOM_HEADER, ReportCategory.PARTIAL_LOADING, out fullSize);
            }
        }

        private static bool TryReadHeaders(HTTPCache cache, Hash128 hash, out WebRequestHeaders headers)
        {
            try
            {
                using Stream? headersStream = HTTPManager.IOService.CreateFileStream(cache.GetHeaderPathFromHash(hash), FileStreamModes.OpenRead);

                headers = CreateEmpty();
                Http2Utils.LoadHeaders(headersStream, headers.value);
                return true;
            }
            catch (Exception e)
            {
                headers = default(WebRequestHeaders);
                ReportHub.LogError(ReportCategory.PARTIAL_LOADING, $"Failed to read headers from cache: {e}");
                return false;
            }
        }

        internal static long CalculateEffectiveChunkSize(long desiredChunkSize, long fullFileSize, byte maxChunksCount)
        {
            double maxChunkSize = fullFileSize / (double)maxChunksCount;

            if (maxChunkSize <= desiredChunkSize)
                return desiredChunkSize;

            // Align maxChunkSize so it's divisible by the desiredChunkSize without remainder
            long alignedChunkSize = (long)Math.Ceiling(maxChunkSize / desiredChunkSize) * desiredChunkSize;

            return alignedChunkSize;
        }

        /// <summary>
        ///     Called when the downloading of the next chunk has started
        /// </summary>
        /// <returns>False if content length could not be resolved</returns>
        public static bool TryInitializeFromHeaders(HTTPCache cache, Uri uri, HTTPMethods method, int statusCode, LoggingContext? loggingContext,
            WebRequestHeaders headers, ref Http2PartialDownloadDataStream? partialStream, long chunkAlignment, byte maxChunksCount, out long expectedChunkLength)
        {
            using ProfilerMarker.AutoScope _ = TRY_INITIALIZE_FROM_HEADERS_MARKER.Auto();

            if (!TryPrepareHeaders(headers, out long fullFileSize, out expectedChunkLength))
            {
                // if headers are invalid we can't proceed
                headers.Dispose();
                return false;
            }

            // Convert 206 to 200 (Cache does not support partial requests)

            if (statusCode == HTTPStatusCodes.PartialContent)
                statusCode = HTTPStatusCodes.OK;

            partialStream ??= new Http2PartialDownloadDataStream(uri, fullFileSize, CalculateEffectiveChunkSize(chunkAlignment, fullFileSize, maxChunksCount));
            partialStream.InitializeFromHeaders(cache, method, uri, statusCode, loggingContext, headers);
            return true;

            static bool TryPrepareHeaders(WebRequestHeaders headers, out long fullSize, out long expectedLength)
            {
                fullSize = 0;

                if (!TryParseFromRangeHeader(headers.value, out fullSize, out expectedLength))
                {
                    // if there is no "Content-Range" header, the server does not support partial requests
                    // to function uniformly it's possible to fall back to the full file download
                    if (TryParseContentSize(headers.value, out fullSize))
                        expectedLength = fullSize;
                    else return false;
                }

                PreparePartialHeaders(headers.value, fullSize);
                return true;
            }

            static bool TryParseContentSize(Dictionary<string, List<string>> headers, out long contentSize) =>
                Http2Utils.TryParseHeaderLong(headers, CONTENT_LENGTH_HEADER, ReportCategory.PARTIAL_LOADING, out contentSize);

            static bool TryParseFromRangeHeader(Dictionary<string, List<string>> headers, out long fullSize, out long chunkSize)
            {
                string? fullSizeHeaderValueRaw = headers.GetFirstHeaderValue(CONTENT_RANGE_HEADER);

                fullSize = 0;
                chunkSize = 0;

                if (fullSizeHeaderValueRaw == null)
                {
                    ReportHub.LogWarning(ReportCategory.PARTIAL_LOADING, $"{CONTENT_RANGE_HEADER} is not present");
                    return false;
                }

                if (!DownloadHandlersUtils.TryParseContentRange(fullSizeHeaderValueRaw, out fullSize, out chunkSize))
                {
                    ReportHub.LogWarning(ReportCategory.PARTIAL_LOADING, $"{CONTENT_RANGE_HEADER}:{fullSizeHeaderValueRaw} is not in the expected format: \"int/int\"");
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        ///     Initializes the stream with serving from memory with no assumptions about the content size
        /// </summary>
        public static Http2PartialDownloadDataStream InitializeFromUnknownSource(Uri url)
        {
            var stream = new Http2PartialDownloadDataStream(url, int.MaxValue, 0);

            SeekableBufferSegmentStream memoryStream = CreateMemoryStream();

            stream.memoryStreamPartialData = new MemoryStreamPartialData(memoryStream);
            stream.OpMode = Mode.WRITING_TO_SEGMENTED_STREAM;
            return stream;
        }

        public override async ValueTask DisposeAsync()
        {
            bool fromMainThread = PlayerLoopHelper.IsMainThread;

            // switch to the background thread before disposing the stream as it involves the lock on the database
            if (fromMainThread)
                await UniTask.SwitchToThreadPool();

            // ReSharper disable once MethodHasAsyncOverload
            try { Dispose(); }
            finally
            {
                if (fromMainThread)
                    await UniTask.SwitchToMainThread();
            }
        }

        private void InitializeFromHeaders(HTTPCache cache, HTTPMethods method, Uri uri, int statusCode, LoggingContext? loggingContext,
            WebRequestHeaders headersResult)
        {
            switch (OpMode)
            {
                // No cached data
                case Mode.UNITIALIZED:

                    if (TryReserveCacheSpace(cache, method, uri, statusCode, loggingContext, headersResult.value, out CacheWriteHandler writeHandler))
                    {
                        cachedPartialData = new CachedPartialData(headersResult, cache, HTTPCache.CalculateHash(method, uri));

                        // When the cache is reserved, the stream is open for write
                        cachedPartialData.writeHandler = writeHandler;
                        OpMode = Mode.WRITING_TO_DISK_CACHE;

                        ReportHub.Log(ReportCategory.PARTIAL_LOADING, $"Partial Download Stream {uri} ({cachedPartialData.requestHash}) initialized as {OpMode} "
                                                                      + $"{BytesFormatter.Convert((ulong)cachedPartialData.partialContentLength, BytesFormatter.DataSizeUnit.Byte, BytesFormatter.DataSizeUnit.Kilobyte)} / "
                                                                      + $"{BytesFormatter.Convert((ulong)fullFileSize, BytesFormatter.DataSizeUnit.Byte, BytesFormatter.DataSizeUnit.Kilobyte)} KB");

                        return;
                    }

                    // Not enough space on disk, use the HTTP Stream
                    ReportHub.Log(ReportCategory.PARTIAL_LOADING, $"Disk Cache is full, Partial Downloading of {uri} ({cachedPartialData.requestHash}) will be served from memory");

                    memoryStreamPartialData = new MemoryStreamPartialData(CreateMemoryStream());
                    OpMode = Mode.WRITING_TO_SEGMENTED_STREAM;

                    return;

                // There is cached data
                case Mode.INCOMPLETE_DATA_CACHED:
                    // Headers are already cached - we don't need another copy
                    headersResult.Dispose();

                    // We need to make sure there is enough space on disk to write the whole file
                    // As previously only a part of it was cached
                    // There is no way to resize the cache

                    // Read the file content into the temporary buffer
                    byte[]? buffer = BufferPool.Get(cachedPartialData.partialContentLength, true, loggingContext);
                    BufferSegment bufferSegment = buffer.AsBuffer((int)cachedPartialData.partialContentLength);

                    using (AutoReleaseBuffer _ = buffer.AsAutoRelease())
                    {
                        cachedPartialData.readHandler!.Value.stream.Read(buffer);
                        cachedPartialData.EndCacheRead(loggingContext, true);

                        if (TryReserveCacheSpace(cache, method, uri, statusCode, loggingContext, cachedPartialData.headers.value, out writeHandler))
                        {
                            // When the cache is reserved, the stream is open for write
                            cachedPartialData.writeHandler = writeHandler;

                            // Copy buffered data to the write handler

                            writeHandler.contentWriter.Write(bufferSegment);

                            OpMode = Mode.WRITING_TO_DISK_CACHE;

                            ReportHub.Log(ReportCategory.PARTIAL_LOADING, $"Partial Download Stream {uri} ({cachedPartialData.requestHash}) initialized as {OpMode} "
                                                                          + $"{BytesFormatter.Convert((ulong)cachedPartialData.partialContentLength, BytesFormatter.DataSizeUnit.Byte, BytesFormatter.DataSizeUnit.Kilobyte)} / "
                                                                          + $"{BytesFormatter.Convert((ulong)fullFileSize, BytesFormatter.DataSizeUnit.Byte, BytesFormatter.DataSizeUnit.Kilobyte)} KB");

                            return;
                        }

                        // Not enough space on disk, use the HTTP Stream
                        cachedPartialData.headers.Dispose();
                        cachedPartialData = default(CachedPartialData);

                        SeekableBufferSegmentStream memoryStream = CreateMemoryStream();

                        // Copy buffered data to the memory stream
                        memoryStream.Write(bufferSegment);

                        memoryStreamPartialData = new MemoryStreamPartialData(memoryStream);
                        OpMode = Mode.WRITING_TO_SEGMENTED_STREAM;

                        ReportHub.Log(ReportCategory.PARTIAL_LOADING,
                            $"Disk Cache is full, Partial Downloading of {uri} ({cachedPartialData.requestHash}) will be served from memory\n"
                            + $"{((ulong)memoryStream.Length).ByteToMB()} MB copied from the previously cached data");

                        return;
                    }

                default:
                    headersResult.Dispose();

                    // Otherwise the stream was already initialized
                    return;
            }
        }

        /// <summary>
        ///     Append the next chunk of partial data
        /// </summary>
        internal bool TryAppend(Uri uri, BufferSegment segment, LoggingContext? loggingContext)
        {
            using ProfilerMarker.AutoScope _ = TRY_APPEND_MARKER.Auto();

            // Read all available data from the response

            switch (OpMode)
            {
                case Mode.WRITING_TO_DISK_CACHE:
                    // Make sure the writer is open

                    if (cachedPartialData.writeHandler == null)
                    {
                        ReportHub.LogError(ReportCategory.PARTIAL_LOADING, "Cache is not open for writing");
                        return false;
                    }

                    HTTPCacheContentWriter cacheWriter = cachedPartialData.writeHandler.Value.contentWriter;

                    cacheWriter.Write(segment, true);

                    // If something went wrong with the cache it will invalidate its hash
                    if (!cacheWriter.Hash.isValid)
                    {
                        ReportHub.LogError(ReportCategory.PARTIAL_LOADING, $"{uri} ({cachedPartialData.requestHash}) Error occured in the cache writer while processing the segment");
                        return false;
                    }

                    var processedLength = (long)cacheWriter.ProcessedLength;

                    if (processedLength > fullFileSize)
                    {
                        ReportHub.LogError(ReportCategory.PARTIAL_LOADING, $"{uri} ({cachedPartialData.requestHash}) Processed length {processedLength} is greater than the full file size {fullFileSize}");
                        return false;
                    }

                    cachedPartialData.partialContentLength = (long)cacheWriter.ProcessedLength;

                    // Try to finalize download

                    if (processedLength == fullFileSize)
                    {
                        // Finish the cache write
                        // Can return false, probably due to the concurrent maintenance
                        bool streamOpened = cachedPartialData.ReadAfterCompletingCacheWrite(loggingContext, false);

                        if (!streamOpened)
                        {
                            // Finish disposal here as the cache entry now is in the intermediate state that can't be properly processed by Dispose
                            cachedPartialData.headers.Dispose();
                            OpMode = Mode.UNITIALIZED;

                            ReportHub.LogWarning(ReportCategory.PARTIAL_LOADING, $"Partial Download Stream {uri} ({cachedPartialData.requestHash}) could not be opened for reading after writing to the cache");
                            return false;
                        }

                        OpMode = Mode.COMPLETE_DATA_CACHED;

                        ReportHub.Log(ReportCategory.PARTIAL_LOADING, $"Partial Download Stream {uri} ({cachedPartialData.requestHash}) completed in {OpMode} mode");
                    }

                    return true;

                case Mode.WRITING_TO_SEGMENTED_STREAM:
                    // Check the size before writing to the stream as here the flow should be exception-free

                    long streamLength = memoryStreamPartialData.stream.Length;

                    long segmentLength = segment.Count;

                    if ((streamLength += segmentLength) > fullFileSize)
                    {
                        ReportHub.LogError(ReportCategory.PARTIAL_LOADING, $"Processed length {streamLength} is greater than the full file size {fullFileSize}");
                        return false;
                    }

                    memoryStreamPartialData.stream.Write(segment);

                    if (streamLength == fullFileSize)
                    {
                        OpMode = Mode.COMPLETE_SEGMENTED_STREAM;
                        ReportHub.Log(ReportCategory.PARTIAL_LOADING, $"Partial Download Stream {uri} completed in {OpMode} mode");
                    }

                    return true;
                default:
                    LogImproperState(nameof(TryAppend));
                    return false;
            }
        }

        /// <summary>
        ///     Forcing finalization is needed if we could make any assumptions about the content size
        /// </summary>
        internal void ForceFinalize()
        {
            if (OpMode == Mode.WRITING_TO_SEGMENTED_STREAM && fullFileSize == int.MaxValue)
            {
                OpMode = Mode.COMPLETE_SEGMENTED_STREAM;
                return;
            }

            LogImproperState(nameof(ForceFinalize), $"Can be invoked only without the full file size derived, but it was set to {fullFileSize}");
        }

        private void LogImproperState(string funcName, string? context = null)
        {
            ReportHub.LogError(ReportCategory.PARTIAL_LOADING, $"{funcName} can't be invoked in the current state: {OpMode}\n{context}");
        }

        /// <summary>
        ///     Discards cached results and disposes the stream
        /// </summary>
        internal void DiscardAndDispose()
        {
            // If the cache is complete, don't discard the data
            switch (OpMode)
            {
                case Mode.INCOMPLETE_DATA_CACHED:
                case Mode.WRITING_TO_DISK_CACHE:
                    discardOnDisposal = true;
                    break;
            }

            Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            using ProfilerMarker.AutoScope _ = DISPOSE_MARKER.Auto();

            base.Dispose(disposing);

            Hash128 hash = default;

            switch (OpMode)
            {
                case Mode.COMPLETE_DATA_CACHED:
                case Mode.INCOMPLETE_DATA_CACHED:
                    hash = cachedPartialData.requestHash;
                    cachedPartialData.EndCacheRead(null, discardOnDisposal);
                    cachedPartialData.headers.Dispose();
                    break;
                case Mode.WRITING_TO_DISK_CACHE:
                    hash = cachedPartialData.requestHash;
                    cachedPartialData.EndCacheWrite(null, discardOnDisposal);
                    cachedPartialData.headers.Dispose();
                    break;
                case Mode.WRITING_TO_SEGMENTED_STREAM:
                case Mode.COMPLETE_SEGMENTED_STREAM:
                    memoryStreamPartialData.stream.Dispose();
                    break;
                case Mode.EMBEDDED_FILE_STREAM:
                    fileStreamData.stream.Dispose();
                    break;
            }

            ReportHub.Log(ReportCategory.PARTIAL_LOADING, $"Partial Download Stream {fromUrl} ({hash}) disposed in {OpMode} mode");

            cachedPartialData = default(CachedPartialData);
            memoryStreamPartialData = default(MemoryStreamPartialData);
            fileStreamData = default(FileStreamData);
            OpMode = Mode.UNITIALIZED;
        }

        /// <summary>
        ///     Prepares headers as <see cref="HTTPCache" /> expects
        /// </summary>
        private static void PreparePartialHeaders(Dictionary<string, List<string>> requestHeaders, long fullFileSize)
        {
            // Remove all headers that are not in CACHE_CONTROL_HEADERS list
            using PooledObject<List<string>> pooledList = REDUNDANT_HEADERS_POOL.Get(out List<string>? toDelete);

            foreach (string header in requestHeaders.Keys)
            {
                if (!CACHE_CONTROL_HEADERS.Contains(header, StringComparer.OrdinalIgnoreCase))
                    toDelete.Add(header);
            }

            foreach (string header in toDelete)
                requestHeaders.Remove(header);

            // Add custom headers
            // Original Content-Length header is ignored by the cache
            requestHeaders.SetHeader(PARTIAL_CONTENT_FULL_LENGTH_CUSTOM_HEADER, fullFileSize.ToString()); // Reserve enough memory for the whole file
        }

        /// <summary>
        ///     Tries to reserve the space for the whole file in the cache and keeps the file open for write expecting the next chunks
        /// </summary>
        private bool TryReserveCacheSpace(HTTPCache cache, HTTPMethods method, Uri uri, int statusCode, LoggingContext? loggingContext,
            Dictionary<string, List<string>> cacheHeaders, out CacheWriteHandler writeHandler)
        {
            // It doesn't take into consideration parallel requests, so in reality the total cache size may be larger than the designated capacity by the payload size of the parallel requests.
            // It's the same problem in the original usage in BestHTTP itself, so we ignore it here
            HTTPCacheContentWriter? writer = cache.BeginCache(method, uri, statusCode, cacheHeaders, loggingContext);

            // Writer will be null if an error occurred, the file is served from "file://" or there is not enough space on disk
            writeHandler = default(CacheWriteHandler);

            if (writer == null)
                return false;

            writeHandler = new CacheWriteHandler(writer);
            return true;
        }

        private static SeekableBufferSegmentStream CreateMemoryStream() =>
            new ();

        internal struct FileStreamData
        {
            internal readonly FileStream stream;

            public FileStreamData(FileStream stream)
            {
                this.stream = stream;
            }
        }

        internal struct MemoryStreamPartialData
        {
            internal readonly SeekableBufferSegmentStream stream;

            public MemoryStreamPartialData(SeekableBufferSegmentStream stream)
            {
                this.stream = stream;
            }
        }

        internal readonly struct CacheWriteHandler
        {
            internal readonly HTTPCacheContentWriter contentWriter;

            public CacheWriteHandler(HTTPCacheContentWriter contentWriter)
            {
                this.contentWriter = contentWriter;
            }
        }

        internal readonly struct CacheReadHandler
        {
            internal readonly Stream stream;

            public CacheReadHandler(Stream stream)
            {
                this.stream = stream;
            }
        }

        internal struct CachedPartialData
        {
            internal readonly WebRequestHeaders headers;

            private readonly HTTPCache cache;
            internal readonly Hash128 requestHash;

            /// <summary>
            ///     Partial Content Length is incremented by the size of the last chunk
            /// </summary>
            internal long partialContentLength;
            internal CacheWriteHandler? writeHandler;
            internal CacheReadHandler? readHandler;

            public CachedPartialData(WebRequestHeaders headers, HTTPCache cache, Hash128 requestHash) : this()
            {
                this.headers = headers;
                this.cache = cache;
                this.requestHash = requestHash;
            }

            private void DeleteCacheEntry(LoggingContext? context = null)
            {
                cache.Delete(requestHash, false, context);
            }

            internal bool BeginCacheRead(LoggingContext? context = null)
            {
                Stream? stream = cache.BeginReadContent(requestHash, context);

                if (stream == null)
                    return false;

                readHandler = new CacheReadHandler(stream);
                return true;
            }

            internal void EndCacheRead(LoggingContext? loggingContext, bool discardCache)
            {
                readHandler!.Value.stream.Dispose();
                cache.EndReadContent(requestHash, loggingContext);
                readHandler = null;

                if (discardCache)
                    DeleteCacheEntry(loggingContext);
            }

            internal bool ReadAfterCompletingCacheWrite(LoggingContext? loggingContext, bool discardCache)
            {
                if (!discardCache)
                    UpdateCacheHeaders(loggingContext);

                Stream? stream = cache.EndCacheAndBeginReadContent(writeHandler!.Value.contentWriter, !discardCache, loggingContext);

                writeHandler = null;

                if (stream == null) return false;

                readHandler = new CacheReadHandler(stream);
                return true;
            }

            internal void EndCacheWrite(LoggingContext? loggingContext, bool discardCache)
            {
                if (!discardCache)
                    UpdateCacheHeaders(loggingContext);

                cache.EndCache(writeHandler!.Value.contentWriter, !discardCache, loggingContext);
                writeHandler = null;
            }

            /// <summary>
            ///     Updates the cache headers with the most recent partial content length
            ///     Flush headers only once when the download has fully finished or the stream is disposed
            /// </summary>
            internal void UpdateCacheHeaders(LoggingContext? loggingContext)
            {
                headers.value.SetHeader(PARTIAL_CONTENT_LENGTH_CUSTOM_HEADER, partialContentLength.ToString());
                cache.RefreshHeaders(requestHash, headers.value, loggingContext);
            }
        }

#region Stream Contract
        public override bool CanRead => IsFullyDownloaded;

        public override bool CanSeek => IsFullyDownloaded;

        public override bool CanWrite => false;

        public override long Length => underlyingStream.Length;

        public override long Position
        {
            get => underlyingStream.Position;
            set => underlyingStream.Position = value;
        }

        internal Stream underlyingStream
        {
            get
            {
                Stream? stream = OpMode switch
                                 {
                                     Mode.COMPLETE_DATA_CACHED => cachedPartialData.readHandler!.Value.stream,
                                     Mode.COMPLETE_SEGMENTED_STREAM => memoryStreamPartialData.stream,
                                     Mode.EMBEDDED_FILE_STREAM => fileStreamData.stream,
                                     _ => throw new InvalidOperationException("The stream is not fully downloaded yet"),
                                 };

                if (stream == null)
                    throw new InvalidOperationException($"The underlying stream created from {fromUrl} is `null` in the mode {OpMode}");

                return stream;
            }
        }

        public override void Flush()
        {
            underlyingStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            underlyingStream.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) =>
            underlyingStream.Seek(offset, origin);

        public override void SetLength(long value)
        {
            underlyingStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("The stream is read-only");
        }

        public override string ToString() =>
            $"{nameof(Http2PartialDownloadDataStream)} {fromUrl} ({cachedPartialData.requestHash})";
#endregion
    }
}
