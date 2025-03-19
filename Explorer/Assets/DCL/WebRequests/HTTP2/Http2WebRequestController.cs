using Best.HTTP;
using Best.HTTP.Caching;
using Best.HTTP.Shared;
using Best.HTTP.Shared.Extensions;
using Best.HTTP.Shared.Logger;
using Best.HTTP.Shared.PlatformSupport.FileSystem;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Pool;
using static DCL.Optimization.Pools.PoolExtensions;

namespace DCL.WebRequests.HTTP2
{
    public class Http2WebRequestController
    {
        // We use custom headers because default ones are not supported by HTTPCache
        private const string ACCEPT_RANGES_HEADER = "Accept-Ranges";
        private const string BYTES_RANGE_HEADER = "bytes";

        private const string CONTENT_RANGE_HEADER = "Content-Range";

        // private const string PARTIAL_CONTENT_FULL_LENGTH_CUSTOM_HEADER = "Partial-Full-Content-Length";

        private readonly HTTPCache cache;

        public Http2WebRequestController(HTTPCache cache)
        {
            this.cache = cache;
        }

        /// <summary>
        ///     "content-range" header is not supported by <see cref="HTTPCache" /> by default so circumvent it manually
        /// </summary>
        /// <returns></returns>
        private CachedPartialData? TryLoadPartialDataFromCache(Uri uri, HTTPMethods method)
        {
            Hash128 hash = HTTPCache.CalculateHash(method, uri);

            // Check if it is cached
            if (!cache.AreCacheFilesExists(hash))
                return null;

            // Start reading from the cache acquiring a lock so the entry won't be deleted on maintenance
            // Thus, we increase the read lock so we need to release it later
            // The locks works for both Headers and Content
            Stream? stream = cache.BeginReadContent(hash, null);

            Scope<Dictionary<string, List<string>>> headers = ReadHeaders(hash, null);

            return stream == null ? null : new CachedPartialData(headers, stream);
        }

        private Scope<Dictionary<string, List<string>>> ReadHeaders(Hash128 hash, LoggingContext? context)
        {
            using Stream? headersStream = HTTPManager.IOService.CreateFileStream(cache.GetHeaderPathFromHash(hash), FileStreamModes.OpenRead);

            Scope<Dictionary<string, List<string>>> pooledHeaders = HEADERS_POOL.AutoScope();

            Http2Utils.LoadHeaders(headersStream, pooledHeaders.Value);

            return pooledHeaders;
        }

        /// <summary>
        ///     Appends a newly received chunk to the existing cached partial data, and writes everything to cache
        /// </summary>
        private void WritePartialDataToCache(Hash128 hash, HTTPResponse response, CachedPartialData? cachedPartialData)
        {
            // Consider the following situations:
            // 1. There is no cached Stream
            // 2. The final result can't be written because there is not enough space of disk

            // At any point we are not going to keep the whole stream in memory

            // Prepare and Write headers

            HTTPCacheContentWriter? cacheWriter = cache.BeginCache();

            cacheWriter.Write();
        }

        private PooledObject<Dictionary<string, List<string>>> PreparePartialHeaders(HTTPResponse nextChunkResponse, string fullFileSize, int cachedPartialContentLength)
        {
            // Get a dictionary from the pool
            PooledObject<Dictionary<string, List<string>>> pooledHeaders = DictionaryPool<string, List<string>>.Get(out Dictionary<string, List<string>>? dict);

            // Copy the headers related to caching
            for (var i = 0; i < CACHE_CONTROL_HEADERS.Length; i++)
            {
                string header = CACHE_CONTROL_HEADERS[i];

                List<string>? headerFromResponse = nextChunkResponse.GetHeaderValues(header);

                if (headerFromResponse != null)
                    dict.Add(header, headerFromResponse);
            }

            // Add custom headers
            dict.AddHeader(CONTENT_LENGTH_HEADER, fullFileSize); // Reserve enough memory

            //var newChunkSize = cachedPartialContentLength + nextChunkResponse.GetFirstHeaderValue(CONTENT_LENGTH_HEADER);

            //dict.AddHeader(PARTIAL_CONTENT_LENGTH_CUSTOM_HEADER, )

            return pooledHeaders;
        }
    }
}
