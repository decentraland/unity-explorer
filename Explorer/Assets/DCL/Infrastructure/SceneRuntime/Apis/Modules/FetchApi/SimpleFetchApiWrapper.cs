using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using JetBrains.Annotations;
using Microsoft.ClearScript;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace SceneRuntime.Apis.Modules.FetchApi
{
    public class SimpleFetchApiWrapper : JsApiWrapper<ISimpleFetchApi>
    {
        private readonly IWebRequestController webController;
        private readonly bool isLocalSceneDevelopment;

        public SimpleFetchApiWrapper(ISimpleFetchApi api, IWebRequestController webController, CancellationTokenSource disposeCts, bool isLocalSceneDevelopment) : base(api, disposeCts)
        {
            this.webController = webController;
            this.isLocalSceneDevelopment = isLocalSceneDevelopment;
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/SimpleFetchApi.js")]
        public object Fetch(string requestMethod, string url, object headers, bool hasBody, string body,
            string redirect, int timeout)
        {
            return FetchAsync(disposeCts.Token).ToDisconnectedPromise(this);

            async UniTask<ResponseToJs> FetchAsync(CancellationToken ct)
            {
                ISimpleFetchApi.Response response = await api.FetchAsync(requestMethod, url, headers, hasBody, body, redirect, timeout, webController, ct, isLocalSceneDevelopment);

                var headersToJs = new PropertyBag();

                if (response.Headers != null)
                    foreach (KeyValuePair<string, string> header in response.Headers)
                        headersToJs.Add(header.Key, header.Value);

                return new ResponseToJs
                {
                    data = response.Data,
                    headers = headersToJs,
                    ok = response.Ok,
                    redirected = response.Redirected,
                    status = response.Status,
                    statusText = response.StatusText,
                    type = response.Type,
                    url = response.URL,
                };
            }
        }

        [Serializable]
        [PublicAPI]
        public struct ResponseToJs
        {
            public bool ok;
            public bool redirected;
            public int status;
            public string statusText;
            public string url;
            public string data;
            public string type;
            public PropertyBag headers;
        }
    }
}
