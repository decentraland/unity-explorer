using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;

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
