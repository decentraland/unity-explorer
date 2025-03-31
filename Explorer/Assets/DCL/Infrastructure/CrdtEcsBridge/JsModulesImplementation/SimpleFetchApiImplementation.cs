using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using Microsoft.ClearScript;
using SceneRuntime.Apis.Modules.FetchApi;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using UnityEngine.Networking;

namespace CrdtEcsBridge.JsModulesImplementation
{
    public class SimpleFetchApiImplementation : ISimpleFetchApi
    {
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private enum RequestMethod
        {
            GET,
            POST,
            PUT,
            DELETE,
            PATCH,
            HEAD,
            INVALID,
        }

        private readonly SceneShortInfo sceneShortInfo;

        public SimpleFetchApiImplementation(SceneShortInfo sceneShortInfo)
        {
            this.sceneShortInfo = sceneShortInfo;
        }

        public void Dispose() { }

        public async UniTask<ISimpleFetchApi.Response> FetchAsync(
            string requestMethod,
            string url,
            object headers,
            bool hasBody,
            string body,
            string redirect,
            int timeout,
            IWebRequestController webController,
            CancellationToken ct,
            bool isLocalSceneDevelopment
        )
        {
            try
            {
                // if we're in LocalSceneDevelopment mode to allow connecting to unsafe websocket server to the client
                if (!isLocalSceneDevelopment && !url.ToLower().StartsWith("https://"))
                    throw new Exception("Can't make an unsafe http request, please upgrade to https. url=" + url);

                RequestMethod parsedRequestMethod = ParseRequestMethod(requestMethod);

                if (parsedRequestMethod == RequestMethod.INVALID)
                    throw new ArgumentException("Invalid request method.");

                var commonArguments = new CommonArguments(URLAddress.FromString(url), timeout: timeout);
                WebRequestHeadersInfo webRequestHeaders = HeadersFromJsObject(headers);

                await UniTask.SwitchToMainThread();

                switch (parsedRequestMethod)
                {
                    case RequestMethod.GET:
                        return await webController.GetAsync(commonArguments, GetReportData(), webRequestHeaders).ToSimpleFetchResponseAsync(ct);
                    case RequestMethod.POST:
                        string postContentType = webRequestHeaders.HeaderContentType();
                        var postArguments = GenericUploadArguments.Create(body, postContentType);
                        return await webController.PostAsync(commonArguments, postArguments, GetReportData(), webRequestHeaders).ToSimpleFetchResponseAsync(ct);
                    case RequestMethod.PUT:
                        string putContentType = webRequestHeaders.HeaderContentType();
                        var putArguments = GenericUploadArguments.Create(body, putContentType);
                        return await webController.PutAsync(commonArguments, putArguments, GetReportData(), webRequestHeaders).ToSimpleFetchResponseAsync(ct);
                    case RequestMethod.DELETE:
                        string deleteContentType = webRequestHeaders.HeaderContentType();
                        var deleteArguments = GenericUploadArguments.Create(body, deleteContentType);
                        return await webController.DeleteAsync(commonArguments, deleteArguments, GetReportData(), webRequestHeaders).ToSimpleFetchResponseAsync(ct);
                    case RequestMethod.PATCH:
                        string patchContentType = webRequestHeaders.HeaderContentType();
                        var patchArguments = GenericUploadArguments.Create(body, patchContentType);
                        return await webController.PatchAsync(commonArguments, patchArguments, GetReportData(), webRequestHeaders).ToSimpleFetchResponseAsync(ct);
                    case RequestMethod.HEAD: throw new NotImplementedException();
                    case RequestMethod.INVALID:
                    default: throw new ArgumentOutOfRangeException();
                }
            }
            catch (WebRequestException e)
            {
                return new ISimpleFetchApi.Response
                {
                    Ok = false,
                    Status = e.ResponseCode,
                    StatusText = e.ResponseCode.ToString(),
                    Data = e.Text,
                    Headers = e.ResponseHeaders,
                };
            }
        }

        private ReportData GetReportData() =>
            new (ReportCategory.SCENE_FETCH_REQUEST, sceneShortInfo: sceneShortInfo);

        private static WebRequestHeadersInfo HeadersFromJsObject(object headers)
        {
            var webRequestHeaders = new WebRequestHeadersInfo();

            if (headers is IScriptObject scriptObject)
            {
                IEnumerable<string> propertyNames = scriptObject.PropertyNames.EnsureNotNull();

                foreach (string name in propertyNames)
                {
                    var property = scriptObject.GetProperty(name).EnsureNotNull().ToString()!;
                    webRequestHeaders.Add(name, property);
                }
            }

            return webRequestHeaders;
        }

        private static RequestMethod ParseRequestMethod(string request) =>
            Enum.TryParse(request, true, out RequestMethod method) ? method : RequestMethod.INVALID;
    }
}
