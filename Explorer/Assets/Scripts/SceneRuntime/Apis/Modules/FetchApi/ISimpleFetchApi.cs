using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;

namespace SceneRuntime.Apis.Modules.FetchApi
{
    public interface ISimpleFetchApi : IDisposable
    {
        public UniTask<Response> FetchAsync(string requestMethod, string url, object headers, bool hasBody, string body,
            string redirect, int timeout, IWebRequestController webController, CancellationToken ct);

        public struct Response
        {
            public Dictionary<string, string> Headers;
            public bool Ok;
            public bool Redirected;
            public int Status;
            public string StatusText;
            public string URL;
            public string Data;
            public string Type;
        }
    }
}
