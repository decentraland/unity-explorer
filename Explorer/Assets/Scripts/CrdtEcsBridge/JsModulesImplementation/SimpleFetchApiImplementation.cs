using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using Newtonsoft.Json;
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
            PATCH,
            HEAD,
            INVALID,
        }

        public void Dispose() { }

        public async UniTask<object> FetchAsync(
            string requestMethod,
            string url,
            object headers,
            bool hasBody,
            string body,
            string redirect,
            int timeout,
            IWebRequestController webController,
            CancellationToken ct
        )
        {
            RequestMethod parsedRequestMethod = ParseRequestMethod(requestMethod);

            if (parsedRequestMethod == RequestMethod.INVALID)
                throw new ArgumentException("Invalid request method.");

            var commonArguments = new CommonArguments(URLAddress.FromString(url), timeout: timeout);
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

            await UniTask.SwitchToMainThread();

            switch (parsedRequestMethod)
            {
                case RequestMethod.GET:
                    return await webController.GetAsync<GenerateResponseOp<GenericGetRequest>, object>(commonArguments, new GenerateResponseOp<GenericGetRequest>(), ct, ReportCategory.SCENE_FETCH_REQUEST, webRequestHeaders);
                case RequestMethod.POST:
                    string postContentType = webRequestHeaders.HeaderOrNull("content-type", true) ?? string.Empty;
                    var postArguments = GenericPostArguments.Create(body, postContentType);
                    return await webController.PostAsync<GenerateResponseOp<GenericPostRequest>, object>(commonArguments, new GenerateResponseOp<GenericPostRequest>(), postArguments, ct, ReportCategory.SCENE_FETCH_REQUEST, webRequestHeaders);
                case RequestMethod.PUT:
                    string putContentType = webRequestHeaders.HeaderOrNull("content-type", true) ?? string.Empty;
                    var putArguments = GenericPutArguments.Create(body, putContentType);
                    return await webController.PutAsync<GenerateResponseOp<GenericPutRequest>, object>(commonArguments, new GenerateResponseOp<GenericPutRequest>(), putArguments, ct, ReportCategory.SCENE_FETCH_REQUEST, webRequestHeaders);
                case RequestMethod.PATCH:
                    string patchContentType = webRequestHeaders.HeaderOrNull("content-type", true) ?? string.Empty;
                    var patchArguments = GenericPatchArguments.Create(body, patchContentType);
                    return await webController.PatchAsync<GenerateResponseOp<GenericPatchRequest>, object>(commonArguments, new GenerateResponseOp<GenericPatchRequest>(), patchArguments, ct, ReportCategory.SCENE_FETCH_REQUEST, webRequestHeaders);
                case RequestMethod.HEAD: throw new NotImplementedException();
                case RequestMethod.INVALID:
                default: throw new ArgumentOutOfRangeException();
            }
        }

        private struct GenerateResponseOp<TGenericRequest> : IWebRequestOp<TGenericRequest, object>
            where TGenericRequest : struct, GenericDownloadHandlerUtils.IGenericDownloadHandlerRequest, ITypedWebRequest
        {
            public UniTask<object?> ExecuteAsync(TGenericRequest request, CancellationToken ct)
            {
                UnityWebRequest unityWebRequest = request.UnityWebRequest;
                string responseData = unityWebRequest.downloadHandler?.text ?? string.Empty;

                var responseHeadersDictionary = unityWebRequest.GetResponseHeaders();
                var responseHeadersPropertyBag = new PropertyBag();
                foreach (var header in responseHeadersDictionary)
                    responseHeadersPropertyBag.Add(header.Key, header.Value);

                bool requestOk = unityWebRequest.result == UnityWebRequest.Result.Success;
                bool requestRedirected = unityWebRequest.result is UnityWebRequest.Result.ProtocolError or UnityWebRequest.Result.ConnectionError;
                var requestStatus = (int)unityWebRequest.responseCode;
                var requestStatusText = unityWebRequest.responseCode.ToString();
                string requestUrl = unityWebRequest.url.EnsureNotNull();

                object result = new
                {
                    headers = responseHeadersPropertyBag,
                    ok = requestOk,
                    redirected = requestRedirected,
                    status = requestStatus,
                    statusText = requestStatusText,
                    url = requestUrl,
                    data = responseData,
                    type = "basic" //Handle Response Types properly  type ResponseType = 'basic' | 'cors' | 'default' | 'error' | 'opaque' | 'opaqueredirect'
                };

                return UniTask.FromResult(result)!;
            }
        }

        private static RequestMethod ParseRequestMethod(string request) =>
            Enum.TryParse(request, true, out RequestMethod method) ? method : RequestMethod.INVALID;
    }
}
