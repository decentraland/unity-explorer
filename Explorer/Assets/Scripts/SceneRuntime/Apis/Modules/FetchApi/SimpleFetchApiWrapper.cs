using DCL.WebRequests;
using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Threading;
using System.Threading.Tasks;
using Utility;

namespace SceneRuntime.Apis.Modules.FetchApi
{
    public class SimpleFetchApiWrapper : JsApiWrapperBase<ISimpleFetchApi>
    {
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly IWebRequestController webController;

        public SimpleFetchApiWrapper(ISimpleFetchApi api, IWebRequestController webController) : base(api)
        {
            cancellationTokenSource = new CancellationTokenSource();
            this.webController = webController;
        }

        protected override void DisposeInternal()
        {
            cancellationTokenSource.SafeCancelAndDispose();
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/SimpleFetchApi.js")]
        public object Fetch(string requestMethod, string url, object headers, bool hasBody, string body,
            string redirect, int timeout)
        {
            try
            {
                return api.FetchAsync(requestMethod, url, headers, hasBody, body, redirect, timeout, webController, cancellationTokenSource.Token).ToDisconnectedPromise();
            }
            catch (Exception e) { return Task.FromException(e).ToPromise(); }
        }
    }
}
