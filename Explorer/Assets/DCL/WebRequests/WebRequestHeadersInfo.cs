using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using System.Text;

namespace DCL.WebRequests
{
    public struct WebRequestHeadersInfo : IDisposable
    {
        internal static readonly WebRequestHeadersInfo EMPTY = new ();

        private static readonly ListObjectPool<WebRequestHeader> POOL = new (listInstanceDefaultCapacity: 4);

        private List<WebRequestHeader> value;

        public IReadOnlyList<WebRequestHeader> Value => value;

        public WebRequestHeadersInfo Add(WebRequestHeader header)
        {
            value ??= POOL.Get();
            value.Add(header);
            return this;
        }

        public WebRequestHeadersInfo Add(string key, string value) =>
            Add(new WebRequestHeader(key, value));

        public override readonly string ToString()
        {
            if (value == null) return "WebRequestHeadersInfo: EMPTY";

            var sb = new StringBuilder();
            sb.Append("WebRequestHeadersInfo: ");

            foreach (var webRequestHeader in value)
            {
                sb.Append(webRequestHeader.ToString());
                sb.Append(", ");
            }

            return sb.ToString();
        }

        public void Dispose()
        {
            if (value != null)
            {
                POOL.Release(value);
                value = null;
            }
        }
    }
}
