﻿using Best.HTTP;
using Best.HTTP.Caching;
using Best.HTTP.Response;
using Best.HTTP.Shared;
using Best.HTTP.Shared.Extensions;
using Best.HTTP.Shared.Logger;
using Best.HTTP.Shared.PlatformSupport.FileSystem;
using Best.HTTP.Shared.PlatformSupport.Memory;
using DCL.Diagnostics;
using DCL.Optimization.ThreadSafePool;
using DCL.WebRequests.CustomDownloadHandlers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            /// File is provided directly from the file system and thus it's never partial,
            /// BESTHttp does not provide a file stream directly so it loads it into the memory chunks
            /// </summary>
            // EMBEDDED_FILE_STREAM = 6 TODO special fast path for files
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

        private static readonly ThreadSafeListPool<string> REDUNDANT_HEADERS_POOL =
            new (15, 50);

        //internal static readonly DictionaryObjectPool<string, List<string>> HEADERS_POOL
        //    = new (dictionaryInstanceDefaultCapacity: CACHE_CONTROL_HEADERS.Length + 3, defaultCapacity: 10, equalityComparer: StringComparer.OrdinalIgnoreCase);

        internal readonly int fullFileSize;

        private CachedPartialData cachedPartialData;
        private MemoryStreamPartialData memoryStreamPartialData;
        private FileStreamData fileStreamData;

        public override bool IsFullyDownloaded => opMode is Mode.COMPLETE_DATA_CACHED or Mode.COMPLETE_SEGMENTED_STREAM;

        internal Mode opMode { get; private set; }

        internal long partialContentLength => opMode switch
                                              {
                                                  Mode.COMPLETE_DATA_CACHED or Mode.INCOMPLETE_DATA_CACHED or Mode.WRITING_TO_DISK_CACHE => cachedPartialData.partialContentLength,
                                                  Mode.COMPLETE_SEGMENTED_STREAM or Mode.WRITING_TO_SEGMENTED_STREAM => memoryStreamPartialData.stream.Length,
                                                  _ => 0,
                                              };

        private Http2PartialDownloadDataStream(int fullFileSize)
        {
            this.fullFileSize = fullFileSize;
        }

        internal ref readonly CachedPartialData GetCachedPartialData() =>
            ref cachedPartialData;

        internal ref readonly MemoryStreamPartialData GetMemoryStreamPartialData() =>
            ref memoryStreamPartialData;

        internal ref readonly FileStreamData GetFileStreamData() =>
            ref fileStreamData;

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

            if (!TryReadHeaders(cache, requestHash, out Dictionary<string, List<string>> headers))
            {
                stream.Dispose();
                cache.EndReadContent(requestHash, null);
                return false;
            }

            if (!TryParseCachedContentSize(headers, out int cachedSize, out int fullFileSize))
            {
                stream.Dispose();
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
                       Http2Utils.TryParseHeader(headers, PARTIAL_CONTENT_FULL_LENGTH_CUSTOM_HEADER, ReportCategory.PARTIAL_LOADING, out fullSize);
            }
        }

        private static bool TryReadHeaders(HTTPCache cache, Hash128 hash, out Dictionary<string, List<string>> headers)
        {
            try
            {
                using Stream? headersStream = HTTPManager.IOService.CreateFileStream(cache.GetHeaderPathFromHash(hash), FileStreamModes.OpenRead);

                headers = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                Http2Utils.LoadHeaders(headersStream, headers);
                return true;
            }
            catch (Exception e)
            {
                headers = null!;
                ReportHub.LogError(ReportCategory.PARTIAL_LOADING, $"Failed to read headers from cache: {e}");
                return false;
            }
        }

        /// <summary>
        ///     Called when the downloading of the next chunk has started
        /// </summary>
        /// <returns>False if content length could not be resolved</returns>
        public static bool TryInitializeFromHeaders(HTTPCache cache, HTTPRequest request, Dictionary<string, List<string>> headers, ref Http2PartialDownloadDataStream? partialStream, out int expectedChunkLength)
        {
            if (!TryPrepareHeaders(headers, out int fullFileSize, out expectedChunkLength))
            {
                // if headers are invalid we can't proceed
                return false;
            }

            // Convert 206 to 200 (Cache does not support partial requests)
            int statusCode = request.Response.StatusCode;

            if (statusCode == HTTPStatusCodes.PartialContent)
                statusCode = HTTPStatusCodes.OK;

            partialStream ??= new Http2PartialDownloadDataStream(fullFileSize);
            partialStream.InitializeFromHeaders(cache, request, statusCode, headers);
            return true;

            static bool TryPrepareHeaders(Dictionary<string, List<string>> headers, out int fullSize, out int expectedLength)
            {
                fullSize = 0;

                if (!TryParseFromRangeHeader(headers, out fullSize, out expectedLength))
                {
                    // if there is no "Content-Range" header, the server does not support partial requests
                    // to function uniformly it's possible to fall back to the full file download
                    if (TryParseContentSize(headers, out fullSize))
                        expectedLength = fullSize;
                    else return false;
                }

                PreparePartialHeaders(headers, fullSize);
                return true;
            }

            static bool TryParseContentSize(Dictionary<string, List<string>> headers, out int contentSize) =>
                Http2Utils.TryParseHeader(headers, CONTENT_LENGTH_HEADER, ReportCategory.PARTIAL_LOADING, out contentSize);

            static bool TryParseFromRangeHeader(Dictionary<string, List<string>> headers, out int fullSize, out int chunkSize)
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
                    ReportHub.LogWarning(ReportCategory.PARTIAL_LOADING, $"{CONTENT_RANGE_HEADER} is not in the expected format: \"int/int\"");
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        ///     Initializes the stream with serving from memory with no assumptions about the content size
        /// </summary>
        public static Http2PartialDownloadDataStream InitializeFromUnknownSource()
        {
            var stream = new Http2PartialDownloadDataStream(int.MaxValue);

            SeekableBufferSegmentStream memoryStream = CreateMemoryStream();

            stream.memoryStreamPartialData = new MemoryStreamPartialData(memoryStream);
            stream.opMode = Mode.WRITING_TO_SEGMENTED_STREAM;
            return stream;
        }

        private void InitializeFromHeaders(HTTPCache cache, HTTPRequest request, int statusCode, Dictionary<string, List<string>> headersResult)
        {
            switch (opMode)
            {
                // No cached data
                case Mode.UNITIALIZED:

                    if (TryReserveCacheSpace(cache, request, statusCode, headersResult, out CacheWriteHandler writeHandler))
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
                        cachedPartialData.EndCacheRead(request.Context, true);

                        if (TryReserveCacheSpace(cache, request, statusCode, cachedPartialData.headers, out writeHandler))
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
                    return;
            }
        }

        /// <summary>
        ///     Append the next chunk of partial data
        /// </summary>
        internal bool TryAppend(BufferSegment segment, LoggingContext loggingContext)
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

                    cacheWriter.Write(segment);

                    var processedLength = (int)cacheWriter.ProcessedLength;

                    if (processedLength > fullFileSize)
                    {
                        // delete cache entry if the size has overflown the expectancy
                        cachedPartialData.EndCacheWrite(loggingContext, true);

                        ReportHub.LogError(ReportCategory.PARTIAL_LOADING, $"Processed length {processedLength} is greater than the full file size {fullFileSize}");
                        return false;
                    }

                    cachedPartialData.partialContentLength = (int)cacheWriter.ProcessedLength;

                    // Try to finalize download

                    if (processedLength == fullFileSize)
                    {
                        // Finish the cache write
                        cachedPartialData.EndCacheWrite(loggingContext, false);

                        // Open for reading immediately
                        cachedPartialData.BeginCacheRead(loggingContext);

                        opMode = Mode.COMPLETE_DATA_CACHED;
                    }

                    return true;

                case Mode.WRITING_TO_SEGMENTED_STREAM:
                    // Check the size before writing to the stream as here the flow should be exception-free

                    long streamLength = memoryStreamPartialData.stream.Length;

                    int segmentLength = segment.Count;

                    if ((streamLength += segmentLength) > fullFileSize)
                    {
                        ReportHub.LogError(ReportCategory.PARTIAL_LOADING, $"Processed length {streamLength} is greater than the full file size {fullFileSize}");
                        return false;
                    }

                    memoryStreamPartialData.stream.Write(segment);

                    if (streamLength == fullFileSize)
                        opMode = Mode.COMPLETE_SEGMENTED_STREAM;

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
            if (opMode == Mode.WRITING_TO_SEGMENTED_STREAM && fullFileSize == int.MaxValue)
            {
                opMode = Mode.COMPLETE_SEGMENTED_STREAM;
                return;
            }

            LogImproperState(nameof(ForceFinalize), $"Can be invoked only without the full file size derived, but it was set to {fullFileSize}");
        }

        private void LogImproperState(string funcName, string? context = null)
        {
            ReportHub.LogError(ReportCategory.PARTIAL_LOADING, $"{funcName} can't be invoked in the current state: {opMode}\n{context}");
        }

        private bool discardOnDisposal;

        /// <summary>
        ///     Discards cached results and disposes the stream
        /// </summary>
        internal void DiscardAndDispose()
        {
            // If the cache is complete don't discard the data
            switch (opMode)
            {
                case Mode.INCOMPLETE_DATA_CACHED:
                    discardOnDisposal = true;
                    break;
            }

            Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            switch (opMode)
            {
                case Mode.COMPLETE_DATA_CACHED:
                case Mode.INCOMPLETE_DATA_CACHED:
                    cachedPartialData.EndCacheRead(null, discardOnDisposal);
                    break;
                case Mode.WRITING_TO_DISK_CACHE:
                    cachedPartialData.EndCacheWrite(null, discardOnDisposal);
                    break;
                case Mode.WRITING_TO_SEGMENTED_STREAM:
                case Mode.COMPLETE_SEGMENTED_STREAM:
                    memoryStreamPartialData.stream.Dispose();
                    break;
            }

            cachedPartialData = default(CachedPartialData);
            memoryStreamPartialData = default(MemoryStreamPartialData);
            fileStreamData = default(FileStreamData);
            opMode = Mode.UNITIALIZED;
        }

        /// <summary>
        ///     Prepares headers as <see cref="HTTPCache" /> expects
        /// </summary>
        private static void PreparePartialHeaders(Dictionary<string, List<string>> requestHeaders, int fullFileSize)
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
        private bool TryReserveCacheSpace(HTTPCache cache, HTTPRequest request, int statusCode, Dictionary<string, List<string>> cacheHeaders, out CacheWriteHandler writeHandler)
        {
            HTTPCacheContentWriter? writer = cache.BeginCache(request.MethodType, request.Uri, statusCode, cacheHeaders, request.Context, true);

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
            internal readonly Dictionary<string, List<string>> headers;

            private readonly HTTPCache cache;
            private readonly Hash128 requestHash;

            /// <summary>
            ///     Partial Content Length is incremented by the size of the last chunk
            /// </summary>
            internal int partialContentLength;
            internal CacheWriteHandler? writeHandler;
            internal CacheReadHandler? readHandler;

            public CachedPartialData(Dictionary<string, List<string>> headers, HTTPCache cache, Hash128 requestHash) : this()
            {
                this.headers = headers;
                this.cache = cache;
                this.requestHash = requestHash;
            }

            private void DeleteCacheEntry(LoggingContext? context = null)
            {
                cache.Delete(requestHash, context);
            }

            internal void BeginCacheRead(LoggingContext? context = null)
            {
                Stream? stream = cache.BeginReadContent(requestHash, context);
                Assert.IsNotNull(stream);

                readHandler = new CacheReadHandler(stream);
            }

            internal void EndCacheRead(LoggingContext? loggingContext, bool discardCache)
            {
                readHandler!.Value.stream.Dispose();
                cache.EndReadContent(requestHash, loggingContext);
                readHandler = null;

                if (discardCache)
                    DeleteCacheEntry(loggingContext);
            }

            internal void EndCacheWrite(LoggingContext? loggingContext, bool discardCache)
            {
                if (!discardCache)
                    UpdateCacheHeaders(loggingContext);

                cache.EndCache(writeHandler!.Value.contentWriter, true, loggingContext);
                writeHandler = null;

                if (discardCache)
                    DeleteCacheEntry(loggingContext);
            }

            /// <summary>
            ///     Updates the cache headers with the most recent partial content length
            ///     Flush headers only once when the download has fully finished or the stream is disposed
            /// </summary>
            internal void UpdateCacheHeaders(LoggingContext? loggingContext)
            {
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
