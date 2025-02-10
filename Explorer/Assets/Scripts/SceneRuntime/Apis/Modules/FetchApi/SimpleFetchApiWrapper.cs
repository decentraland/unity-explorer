﻿using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using JetBrains.Annotations;
using Microsoft.ClearScript;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace SceneRuntime.Apis.Modules.FetchApi
{
    public class SimpleFetchApiWrapper : JsApiWrapperBase<ISimpleFetchApi>
    {
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly IWebRequestController webController;

        public SimpleFetchApiWrapper(ISimpleFetchApi api, IWebRequestController webController, bool isPreview) : base(api, isPreview)
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
            return FetchAsync(cancellationTokenSource.Token).ToDisconnectedPromise();

            async UniTask<ResponseToJs> FetchAsync(CancellationToken ct)
            {
                ISimpleFetchApi.Response response = await api.FetchAsync(requestMethod, url, headers, hasBody, body, redirect, timeout, webController, ct, isPreview);

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
