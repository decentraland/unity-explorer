using DCL.WebRequests;
using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Threading;
using System.Threading.Tasks;
using Utility;

namespace SceneRuntime.Apis.Modules.FetchApi
{
    public class SimpleFetchApiWrapper : IJsApiWrapper
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
            cancellationTokenSource.SafeCancelAndDispose();
            api.Dispose();
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/SimpleFetchApi.js")]
        public object Fetch(string requestMethod, string url, object headers, bool hasBody, string body,
            string redirect, int timeout)
        {
            try
            {
                return api.FetchAsync(requestMethod, url, headers, hasBody, body, redirect, timeout, webController, cancellationTokenSource.Token).ToPromise();
            }
            catch (Exception e) { return Task.FromException(e).ToPromise(); }
        }
    }
}
