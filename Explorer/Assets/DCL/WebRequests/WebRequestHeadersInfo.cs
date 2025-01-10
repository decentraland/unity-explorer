﻿using DCL.Optimization.Pools;
using DCL.WebRequests.CustomDownloadHandlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DCL.WebRequests
{
    public struct WebRequestHeadersInfo : IDisposable
    {
        private const string RANGE_HEADER = "Range";
        private const string EMPTY_HEADER = "";

        private static readonly IReadOnlyDictionary<string, string> EMPTY_HEADERS = new Dictionary<string, string>();

        private static readonly ListObjectPool<WebRequestHeader> POOL = new (listInstanceDefaultCapacity: 4);

        private List<WebRequestHeader>? values;

        public readonly IReadOnlyList<WebRequestHeader> Value => values as IReadOnlyList<WebRequestHeader> ?? Array.Empty<WebRequestHeader>();

        public WebRequestHeadersInfo(IReadOnlyDictionary<string, string>? headers)
        {
            values = POOL.Get()!;

            foreach ((string key, string s) in headers ?? EMPTY_HEADERS)
                Add(key, s);
        }

        public WebRequestHeadersInfo WithRange(long start, long end)
        {
            Add(RANGE_HEADER, DownloadHandlersUtils.GetContentRangeHeaderValue(start, end));
            return this;
        }

        internal static WebRequestHeadersInfo NewEmpty() =>
            new ();

        public WebRequestHeadersInfo Add(WebRequestHeader header)
        {
            values ??= POOL.Get()!;
            values.Add(header);
            return this;
        }

        public WebRequestHeadersInfo Add(string key, string value) =>
            Add(new WebRequestHeader(key, value));

        public WebRequestHeadersInfo WithSign(string jsonMetaData, ulong unixTimestamp)
        {
            Add("x-identity-timestamp", unixTimestamp.ToString()!);
            Add("x-identity-metadata", string.IsNullOrEmpty(jsonMetaData) ? "{}" : jsonMetaData);

            return this;
        }

        /// <param name="key">Case sensitive key or use bool for insensitive</param>
        /// <returns>Value of the key or not, if the key doesn't exist</returns>
        public readonly string? HeaderOrNull(string key, bool caseInsensitive = false)
        {
            if (values == null)
                return null;

            foreach (WebRequestHeader header in values)
            {
                if (string.Equals(header.Name, key, caseInsensitive ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture))
                    return header.Value;
            }

            return null;
        }

        public readonly string HeaderOrDefault(
            string key,
            bool caseInsensitive = false,
            string defaultHeader = EMPTY_HEADER
        ) =>
            HeaderOrNull(key, caseInsensitive) ?? defaultHeader;

        public readonly IReadOnlyDictionary<string, string> AsDictionary() =>
            values?.ToDictionary(e => e.Name, e => e.Value) ?? EMPTY_HEADERS;

        public readonly Dictionary<string, string> AsMutableDictionary() =>
            values?.ToDictionary(e => e.Name, e => e.Value) ?? new Dictionary<string, string>();

        public override readonly string ToString()
        {
            if (values == null) return "WebRequestHeadersInfo: EMPTY";

            var sb = new StringBuilder();
            sb.Append("WebRequestHeadersInfo: ");

            foreach (var webRequestHeader in values)
            {
                sb.Append(webRequestHeader.ToString());
                sb.Append(", ");
            }

            return sb.ToString();
        }

        public void Dispose()
        {
            if (values != null)
            {
                POOL.Release(values);
                values = null;
            }
        }
    }

    public static class WebRequestHeadersInfoExtensions
    {
        public static string HeaderContentType(this WebRequestHeadersInfo webRequestHeadersInfo) =>
            webRequestHeadersInfo.HeaderOrDefault("content-type", true);
    }
}
