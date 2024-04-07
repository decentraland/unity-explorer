using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;

namespace SceneRuntime.Apis.Modules
{
    public interface ISimpleFetchApi : IDisposable
    {
        public UniTask<FetchResponse> Fetch(string requestMethod, string url, object headers, bool hasBody, string body,
            string redirect, int timeout, IWebRequestController webController, CancellationToken ct);

        public struct FetchResponse
        {
            public FetchResponse(IReadOnlyDictionary<string, string> headers, bool ok, bool redirected, int status, string statusText,
                string url, string data)
            {
                this.headers = headers;
                this.ok = ok;
                this.redirected = redirected;
                this.status = status;
                this.statusText = statusText;
                this.url = url;
                this.data = data;
            }

            private IReadOnlyDictionary<string, string> headers { get; }
            private bool ok { get; }
            private bool redirected { get; }
            private int status { get; }
            private string statusText { get; }
            private string url { get; }
            private string data { get; }
        }
    }
}
