using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DCL.WebRequests
{
    public struct WebRequestHeadersInfo : IDisposable
    {
        internal static readonly WebRequestHeadersInfo EMPTY = new ();
        private static readonly IReadOnlyDictionary<string, string> EMPTY_HEADERS = new Dictionary<string, string>();

        private static readonly ListObjectPool<WebRequestHeader> POOL = new (listInstanceDefaultCapacity: 4);

        private List<WebRequestHeader>? values;

        public readonly IReadOnlyList<WebRequestHeader> Value => values!;

        public WebRequestHeadersInfo(IReadOnlyDictionary<string, string>? headers)
        {
            values = POOL.Get()!;

            foreach ((string key, string s) in headers ?? EMPTY_HEADERS)
                Add(key, s);
        }

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
            Add("x-identity-metadata", jsonMetaData);
            return this;
        }

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
}
