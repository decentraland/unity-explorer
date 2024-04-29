using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using System;
using System.Threading;

namespace SceneRuntime.Apis.Modules.FetchApi
{
    public interface ISimpleFetchApi : IDisposable
    {
        public UniTask<object> FetchAsync(string requestMethod, string url, object headers, bool hasBody, string body,
            string redirect, int timeout, IWebRequestController webController, CancellationToken ct);

    }
}
