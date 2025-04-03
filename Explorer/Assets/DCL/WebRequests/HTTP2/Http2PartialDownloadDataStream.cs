using Best.HTTP;
using Best.HTTP.Caching;
using Best.HTTP.Shared;
using Best.HTTP.Shared.Extensions;
using Best.HTTP.Shared.Logger;
using Best.HTTP.Shared.PlatformSupport.FileSystem;
using Best.HTTP.Shared.PlatformSupport.Memory;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using DCL.WebRequests.CustomDownloadHandlers;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
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
        internal enum Mode
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
            /// File is provided directly from the file system and thus it's never partial
            /// </summary>
            // EMBEDDED_FILE_STREAM = 6 TODO special fast path for files
        }

        internal const string PARTIAL_CONTENT_LENGTH_CUSTOM_HEADER = "Partial-Content-Length";

        private static readonly string[] CACHE_CONTROL_HEADERS =
        {
            "cache-control",
            "etag",
            "expires",
            "last-modified",
        };
        internal static readonly DictionaryObjectPool<string, List<string>> HEADERS_POOL
            = new (dictionaryInstanceDefaultCapacity: CACHE_CONTROL_HEADERS.Length + 3, defaultCapacity: 10);

        internal readonly int fullFileSize;

        private Mode opMode;
        private CachedPartialData cachedPartialData;
        private MemoryStreamPartialData memoryStreamPartialData;
        private FileStreamData fileStreamData;

        public override bool IsFullyDownloaded => opMode is Mode.COMPLETE_DATA_CACHED or Mode.COMPLETE_SEGMENTED_STREAM;

        internal long partialContentLength => opMode switch
                                              {
                                                  Mode.COMPLETE_DATA_CACHED or Mode.INCOMPLETE_DATA_CACHED or Mode.WRITING_TO_DISK_CACHE => cachedPartialData.partialContentLength,
                                                  Mode.COMPLETE_SEGMENTED_STREAM or Mode.WRITING_TO_SEGMENTED_STREAM => memoryStreamPartialData.stream.Length,
                                                  _ => 0,
                                              };

        public Http2PartialDownloadDataStream(int fullFileSize)
        {
            this.fullFileSize = fullFileSize;
        }

        internal static bool TryInitializeFromCache(HTTPCache cache, Hash128 requestHash, out Http2PartialDownloadDataStream? partialStream)
        {
            partialStream = null;

            // Check if it is cached
            if (!cache.AreCacheFilesExists(requestHash))
                return false;

            // Start reading from the cache acquiring a lock so the entry won't be deleted on maintenance
            // Thus, we increase the read lock so we need to release it later
            // The locks works for both Headers and Content
            Stream stream = cache.BeginReadContent(requestHash, null);

            if (stream == null)
                return false;

            if (!TryReadHeaders(cache, requestHash, out PoolExtensions.Scope<Dictionary<string, List<string>>> headers))
            {
                cache.EndReadContent(requestHash, null);
                return false;
            }

            if (!TryParseCachedContentSize(headers.Value, out int cachedSize, out int fullFileSize))
            {
                cache.EndReadContent(requestHash, null);
                return false;
            }

            partialStream = new Http2PartialDownloadDataStream(fullFileSize);

            partialStream.cachedPartialData = new CachedPartialData(headers, cache, requestHash)
            {
                partialContentLength = cachedSize,
                readHandler = new CacheReadHandler(stream),
            };

            // Set the state - whether it is fully cached or not
            partialStream.opMode = partialStream.cachedPartialData.partialContentLength == fullFileSize
                ? Mode.COMPLETE_DATA_CACHED
                : Mode.INCOMPLETE_DATA_CACHED;

            return true;

            static bool TryParseCachedContentSize(Dictionary<string, List<string>> headers, out int cachedSize, out int fullSize)
            {
                fullSize = 0;

                return Http2Utils.TryParseHeader(headers, PARTIAL_CONTENT_LENGTH_CUSTOM_HEADER, ReportCategory.PARTIAL_LOADING, out cachedSize) &&
                       Http2Utils.TryParseHeader(headers, CONTENT_LENGTH_HEADER, ReportCategory.PARTIAL_LOADING, out fullSize);
            }
        }

        private static bool TryReadHeaders(HTTPCache cache, Hash128 hash, out PoolExtensions.Scope<Dictionary<string, List<string>>> pooledHeaders)
        {
            try
            {
                using Stream? headersStream = HTTPManager.IOService.CreateFileStream(cache.GetHeaderPathFromHash(hash), FileStreamModes.OpenRead);

                pooledHeaders = HEADERS_POOL.AutoScope();
                Http2Utils.LoadHeaders(headersStream, pooledHeaders.Value);
                return true;
            }
            catch (Exception e)
            {
                pooledHeaders = default(PoolExtensions.Scope<Dictionary<string, List<string>>>);
                ReportHub.LogError(ReportCategory.PARTIAL_LOADING, $"Failed to read headers from cache: {e}");
                return false;
            }
        }

        /// <summary>
        ///     Called when the downloading of the next chunk has started
        /// </summary>
        /// <returns>"Range" headers are supported by the server</returns>
        public static bool TryInitializeFromHeaders(HTTPCache cache, HTTPRequest request, HTTPResponse response, ref Http2PartialDownloadDataStream? partialStream)
        {
            if (!TryPrepareHeaders(out PoolExtensions.Scope<Dictionary<string, List<string>>> headersResult, out int fullFileSize))
            {
                // if headers are invalid we can't proceed
                return false;
            }

            partialStream ??= new Http2PartialDownloadDataStream(fullFileSize);
            partialStream.InitializeFromHeaders(cache, request, response, headersResult);
            return true;

            bool TryPrepareHeaders(out PoolExtensions.Scope<Dictionary<string, List<string>>> pooledHeaders, out int fullSize)
            {
                pooledHeaders = default(PoolExtensions.Scope<Dictionary<string, List<string>>>);
                fullSize = 0;

                // if there is no "Content-Range" header, the server does not support partial requests
                // to function uniformly it's possible to fall back to the full file download
                if (!TryParseFullContentSizeFromRangeHeader(response.Headers, out fullSize)
                    || !TryParseContentSize(response.Headers, out fullSize))
                    return false;

                pooledHeaders = HEADERS_POOL.AutoScope();

                PreparePartialHeaders(pooledHeaders.Value, response.Headers, fullSize);
                return true;
            }

            static bool TryParseContentSize(Dictionary<string, List<string>> headers, out int contentSize) =>
                Http2Utils.TryParseHeader(headers, CONTENT_LENGTH_HEADER, ReportCategory.PARTIAL_LOADING, out contentSize);

            static bool TryParseFullContentSizeFromRangeHeader(Dictionary<string, List<string>> headers, out int fullSize)
            {
                string? fullSizeHeaderValueRaw = headers.GetFirstHeaderValue(CONTENT_RANGE_HEADER);

                fullSize = 0;

                if (fullSizeHeaderValueRaw == null)
                {
                    ReportHub.LogWarning(ReportCategory.PARTIAL_LOADING, $"{CONTENT_RANGE_HEADER} is not present");
                    return false;
                }

                if (!DownloadHandlersUtils.TryGetFullSize(fullSizeHeaderValueRaw, out fullSize))
                {
                    ReportHub.LogWarning(ReportCategory.PARTIAL_LOADING, $"{CONTENT_RANGE_HEADER} can't be parsed to \"int\"");
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        ///     Initializes the stream with serving from memory with no assumptions about the content size
        /// </summary>
        public static Http2PartialDownloadDataStream InitializeFromUnknownSource(HTTPRequest request, HTTPResponse response)
        {
            var stream = new Http2PartialDownloadDataStream(int.MaxValue);

            SeekableBufferSegmentStream memoryStream = CreateMemoryStream();

            stream.memoryStreamPartialData = new MemoryStreamPartialData(memoryStream);
            stream.opMode = Mode.WRITING_TO_SEGMENTED_STREAM;
            return stream;
        }

        private void InitializeFromHeaders(HTTPCache cache, HTTPRequest request, HTTPResponse response, PoolExtensions.Scope<Dictionary<string, List<string>>> headersResult)
        {
            switch (opMode)
            {
                // No cached data
                case Mode.UNITIALIZED:

                    if (TryReserveCacheSpace(cache, request, response, headersResult.Value, out CacheWriteHandler writeHandler))
                    {
                        cachedPartialData = new CachedPartialData(headersResult, cache, HTTPCache.CalculateHash(request.MethodType, request.Uri));

                        // When the cache is reserved, the stream is open for write
                        cachedPartialData.writeHandler = writeHandler;
                        opMode = Mode.WRITING_TO_DISK_CACHE;
                        return;
                    }

                    // Not enough space on disk, use the HTTP Stream
                    ReportHub.Log(ReportCategory.PARTIAL_LOADING, $"Disk Cache is full, Partial Downloading of {request.Uri} will be served from memory");

                    memoryStreamPartialData = new MemoryStreamPartialData(CreateMemoryStream());
                    opMode = Mode.WRITING_TO_SEGMENTED_STREAM;

                    return;

                // There is cached data
                case Mode.INCOMPLETE_DATA_CACHED:

                    // We need to make sure there is enough space on disk to write the whole file
                    // As previously only a part of it was cached
                    // There is no way to resize the cache

                    // Read the file content into the temporary buffer
                    byte[]? buffer = BufferPool.Get(cachedPartialData.partialContentLength, true, request.Context);
                    BufferSegment bufferSegment = buffer.AsBuffer(cachedPartialData.partialContentLength);

                    using (AutoReleaseBuffer _ = buffer.AsAutoRelease())
                    {
                        cachedPartialData.readHandler!.Value.stream.Read(buffer);
                        cachedPartialData.EndCacheRead(request.Context);

                        // Delete Cache Entry
                        cachedPartialData.DeleteCacheEntry(request.Context);

                        if (TryReserveCacheSpace(cache, request, response, cachedPartialData.pooledHeaders.Value, out writeHandler))
                        {
                            // When the cache is reserved, the stream is open for write
                            cachedPartialData.writeHandler = writeHandler;
                            opMode = Mode.WRITING_TO_DISK_CACHE;

                            // Copy buffered data to the write handler

                            writeHandler.contentWriter.Write(bufferSegment);
                            return;
                        }

                        ReportHub.Log(ReportCategory.PARTIAL_LOADING, $"Disk Cache is full, Partial Downloading of {request.Uri} will be served from memory");

                        // Not enough space on disk, use the HTTP Stream
                        cachedPartialData = default(CachedPartialData);

                        SeekableBufferSegmentStream memoryStream = CreateMemoryStream();

                        // Copy buffered data to the memory stream
                        memoryStream.Write(bufferSegment);

                        memoryStreamPartialData = new MemoryStreamPartialData(memoryStream);
                        opMode = Mode.WRITING_TO_SEGMENTED_STREAM;
                        return;
                    }

                default:
                    // Otherwise the stream was already initialized
                    LogImproperState(nameof(TryInitializeFromHeaders));
                    return;
            }
        }

        internal bool TryFinalizeDownloading()
        {
            switch (opMode)
            {
                case Mode.UNITIALIZED:
                case Mode.WRITING_TO_DISK_CACHE:
                case Mode.INCOMPLETE_DATA_CACHED:
                    LogImproperState(nameof(TryFinalizeDownloading));
                    return false;
                case Mode.WRITING_TO_SEGMENTED_STREAM:
                    if (fullFileSize == int.MaxValue) // Content size is not determined
                        opMode = Mode.COMPLETE_SEGMENTED_STREAM;
                    else
                    {
                        LogImproperState(nameof(TryFinalizeDownloading));
                        return false;
                    }

                    break;
            }

            return true;
        }

        /// <summary>
        ///     Append the next chunk of partial data
        /// </summary>
        internal bool TryAppend(HTTPResponse response)
        {
            // Read all available data from the response

            switch (opMode)
            {
                case Mode.WRITING_TO_DISK_CACHE:
                    // Make sure writer is open

                    if (cachedPartialData.writeHandler == null)
                    {
                        ReportHub.LogError(ReportCategory.PARTIAL_LOADING, "Cache is not open for writing");
                        return false;
                    }

                    HTTPCacheContentWriter cacheWriter = cachedPartialData.writeHandler.Value.contentWriter;

                    while (response.DownStream.TryTake(out BufferSegment segment))
                        cacheWriter.Write(segment);

                    var processedLength = (int)cacheWriter.ProcessedLength;

                    if (processedLength > fullFileSize)
                    {
                        cachedPartialData.EndCacheWrite(response.Context);

                        // delete cache entry if the size has overflown the expectancy
                        cachedPartialData.DeleteCacheEntry(response.Context);

                        ReportHub.LogError(ReportCategory.PARTIAL_LOADING, $"Processed length {processedLength} is greater than the full file size {fullFileSize}");
                        return false;
                    }

                    cachedPartialData.partialContentLength = (int)cacheWriter.ProcessedLength;

                    // Try to finalize download

                    if (processedLength == fullFileSize)
                    {
                        // Flush headers as the response has finished
                        cachedPartialData.UpdateCacheHeaders(response.Context);

                        // Finish the cache write
                        cachedPartialData.EndCacheWrite(response.Context);

                        // Open for reading immediately
                        cachedPartialData.BeginCacheRead(response.Context);

                        opMode = Mode.COMPLETE_DATA_CACHED;
                    }

                    return true;

                case Mode.WRITING_TO_SEGMENTED_STREAM:
                    // Check the size before writing to the stream as here the flow should be exception-free

                    long streamLength = memoryStreamPartialData.stream.Length;

                    while (response.DownStream.TryTake(out BufferSegment segment))
                    {
                        int segmentLength = segment.Count;

                        if ((streamLength += segmentLength) > fullFileSize)
                        {
                            ReportHub.LogError(ReportCategory.PARTIAL_LOADING, $"{response.Request.Uri}, Processed length {streamLength} is greater than the full file size {fullFileSize}");
                            return false;
                        }

                        memoryStreamPartialData.stream.Write(segment);
                    }

                    if (streamLength == fullFileSize)
                        opMode = Mode.COMPLETE_SEGMENTED_STREAM;

                    return true;
                default:
                    LogImproperState(nameof(TryAppend));
                    return false;
            }
        }

        private void LogImproperState(string funcName)
        {
            ReportHub.LogError(ReportCategory.PARTIAL_LOADING, $"{funcName} can't be invoked in the current state: {opMode}");
        }

        internal void DisposeAndDiscard()
        {
            cachedPartialData.DeleteCacheEntry();
            Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            switch (opMode)
            {
                case Mode.COMPLETE_DATA_CACHED:
                case Mode.INCOMPLETE_DATA_CACHED:
                    cachedPartialData.EndCacheRead(null);
                    cachedPartialData.pooledHeaders.Dispose();
                    break;
                case Mode.WRITING_TO_DISK_CACHE:
                    cachedPartialData.pooledHeaders.Dispose();
                    cachedPartialData.EndCacheWrite(null);
                    break;
                case Mode.WRITING_TO_SEGMENTED_STREAM:
                case Mode.COMPLETE_SEGMENTED_STREAM:
                    memoryStreamPartialData.stream.Dispose();
                    break;
            }

            cachedPartialData = default(CachedPartialData);
            memoryStreamPartialData = default(MemoryStreamPartialData);
            opMode = Mode.UNITIALIZED;
        }

        /// <summary>
        ///     Prepares headers as <see cref="HTTPCache" /> expects
        /// </summary>
        private static void PreparePartialHeaders(Dictionary<string, List<string>> requestHeaders, Dictionary<string, List<string>> newHeaders, int fullFileSize)
        {
            // Copy the headers related to caching
            for (var i = 0; i < CACHE_CONTROL_HEADERS.Length; i++)
            {
                string header = CACHE_CONTROL_HEADERS[i];

                if (newHeaders.HasHeader(header)) continue;

                List<string>? headerFromResponse = requestHeaders.GetHeaderValues(header);
                newHeaders.Add(header, headerFromResponse);
            }

            // Add custom headers
            newHeaders.AddHeader(CONTENT_LENGTH_HEADER, fullFileSize.ToString()); // Reserve enough memory for the whole file
        }

        /// <summary>
        ///     Tries to reserve the space for the whole file in the cache and keeps the file open for write expecting the next chunks
        /// </summary>
        private bool TryReserveCacheSpace(HTTPCache cache, HTTPRequest request, HTTPResponse response, Dictionary<string, List<string>> cacheHeaders, out CacheWriteHandler writeHandler)
        {
            HTTPCacheContentWriter? writer = cache.BeginCache(request.MethodType, request.Uri, response.StatusCode, cacheHeaders, request.Context);

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

        private readonly struct CacheWriteHandler
        {
            internal readonly HTTPCacheContentWriter contentWriter;

            public CacheWriteHandler(HTTPCacheContentWriter contentWriter)
            {
                this.contentWriter = contentWriter;
            }
        }

        private readonly struct CacheReadHandler
        {
            internal readonly Stream stream;

            public CacheReadHandler(Stream stream)
            {
                this.stream = stream;
            }
        }

        private struct CachedPartialData
        {
            internal readonly PoolExtensions.Scope<Dictionary<string, List<string>>> pooledHeaders;

            private readonly HTTPCache cache;
            private readonly Hash128 requestHash;

            /// <summary>
            ///     Partial Content Length is incremented by the size of the last chunk
            /// </summary>
            internal int partialContentLength;
            internal CacheWriteHandler? writeHandler;
            internal CacheReadHandler? readHandler;

            public CachedPartialData(PoolExtensions.Scope<Dictionary<string, List<string>>> pooledHeaders, HTTPCache cache, Hash128 requestHash) : this()
            {
                this.pooledHeaders = pooledHeaders;
                this.cache = cache;
                this.requestHash = requestHash;
            }

            internal void DeleteCacheEntry(LoggingContext? context = null)
            {
                cache.Delete(requestHash, context);
            }

            internal void BeginCacheRead(LoggingContext? context = null)
            {
                readHandler = new CacheReadHandler(cache.BeginReadContent(requestHash, context));
            }

            internal void EndCacheRead(LoggingContext? loggingContext)
            {
                readHandler!.Value.stream.Dispose();
                cache.EndReadContent(requestHash, loggingContext);
                readHandler = null;
            }

            internal void EndCacheWrite(LoggingContext? loggingContext)
            {
                cache.EndCache(writeHandler!.Value.contentWriter, true, loggingContext);
                writeHandler = null;
            }

            /// <summary>
            ///     Updates the cache headers with the most recent partial content length
            ///     Flush headers only once when the download has fully finished or the stream is disposed
            /// </summary>
            internal void UpdateCacheHeaders(LoggingContext? loggingContext)
            {
                Dictionary<string, List<string>> headers = pooledHeaders.Value;
                headers.SetHeader(PARTIAL_CONTENT_LENGTH_CUSTOM_HEADER, partialContentLength.ToString());
                cache.RefreshHeaders(requestHash, headers, loggingContext);
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

        private Stream underlyingStream => opMode switch
                                           {
                                               Mode.COMPLETE_DATA_CACHED => cachedPartialData.readHandler!.Value.stream,
                                               Mode.COMPLETE_SEGMENTED_STREAM => memoryStreamPartialData.stream,
                                               _ => throw new InvalidOperationException("The stream is not fully downloaded yet"),
                                           };

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
#endregion
    }
}
