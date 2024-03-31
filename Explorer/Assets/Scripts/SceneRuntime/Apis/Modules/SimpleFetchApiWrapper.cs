using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SceneRuntime.Apis.Modules
{
    public class SimpleFetchApiWrapper : IDisposable
    {
        private readonly ISimpleFetchApi api;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly IWebRequestController webController;

        public SimpleFetchApiWrapper(ISimpleFetchApi api, IWebRequestController webController)
        {
            this.api = api;
            cancellationTokenSource = new CancellationTokenSource();
            this.webController = webController;
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            api.Dispose();
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/SimpleFetchApi.js")]
        public object Fetch(string requestMethod, string url, object headers, bool hasBody, string body,
            string redirect, int timeout)
        {
            try { return api.Fetch(requestMethod, url, headers, hasBody, body, redirect, timeout, webController, cancellationTokenSource.Token).AsTask().ToPromise(); }
            catch (Exception e) { return Task.FromException(e).ToPromise(); }
        }
    }
}
