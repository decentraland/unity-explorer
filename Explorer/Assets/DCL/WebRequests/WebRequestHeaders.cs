using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;

namespace DCL.WebRequests
{
    /// <summary>
    ///     Adapts headers from either implementation to the dictionary acceptable by the cache
    /// </summary>
    public struct WebRequestHeaders : IDisposable
    {
        public const string CONTENT_TYPE_HEADER = "Content-Type";
        internal const string CONTENT_LENGTH_HEADER = "Content-Length";
        internal const string CONTENT_RANGE_HEADER = "Content-Range";
        internal const string ACCEPT_RANGES_HEADER = "Accept-Ranges";
        internal const string BYTES_RANGE_HEADER = "bytes";
        internal const string RANGE_HEADER = "Range";

        private static readonly ThreadSafeDictionaryPool<string, List<string>> HEADERS_POOL
            = new (10, 12, equalityComparer: StringComparer.OrdinalIgnoreCase);

        private static readonly ThreadSafeListPool<string> HEADER_VALUES_POOL = new (1, 12);

        private readonly bool pooled;

        internal readonly Dictionary<string, List<string>> value;

        public WebRequestHeaders(Dictionary<string, List<string>> headers, bool pooled = false)
        {
            value = headers;
            this.pooled = pooled;
        }

        public WebRequestHeaders(HttpResponseMessage response)
        {
            value = HEADERS_POOL.Get();
            pooled = true;

            AddHeaders(value, response.Headers);
            AddHeaders(value, response.Content.Headers);

            void AddHeaders(Dictionary<string, List<string>> collection, HttpHeaders headers)
            {
                foreach (KeyValuePair<string, IEnumerable<string>> header in headers)
                {
                    List<string>? list = HEADER_VALUES_POOL.Get();
                    list.AddRange(header.Value);
                    collection[header.Key] = list;
                }
            }
        }

        public static WebRequestHeaders CreateEmpty() =>
            new (HEADERS_POOL.Get(), true);

        public static bool TryParseUnsigned(string? header, out ulong value)
        {
            value = 0;
            return header != null && ulong.TryParse(header, out value);
        }

        public void Dispose()
        {
            if (!pooled) return;

            foreach (KeyValuePair<string, List<string>> kvp in value)
                HEADER_VALUES_POOL.Release(kvp.Value);

            HEADERS_POOL.Release(value);
        }
    }
}
