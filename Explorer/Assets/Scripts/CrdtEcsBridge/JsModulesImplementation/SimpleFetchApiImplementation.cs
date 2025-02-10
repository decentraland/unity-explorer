﻿using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using DCL.WebRequests.GenericDelete;
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
            bool isPreview
        )
        {
            try
            {
                // if we're in preview mode to allow connecting to unsafe websocket server to the client
                if (!isPreview && !url.ToLower().StartsWith("https://"))
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
                        return await webController.GetAsync<GenerateResponseOp<GenericGetRequest>, ISimpleFetchApi.Response>(commonArguments, new GenerateResponseOp<GenericGetRequest>(), ct, GetReportData(), webRequestHeaders);
                    case RequestMethod.POST:
                        string postContentType = webRequestHeaders.HeaderContentType();
                        var postArguments = GenericPostArguments.Create(body, postContentType);
                        return await webController.PostAsync<GenerateResponseOp<GenericPostRequest>, ISimpleFetchApi.Response>(commonArguments, new GenerateResponseOp<GenericPostRequest>(), postArguments, ct, GetReportData(), webRequestHeaders);
                    case RequestMethod.PUT:
                        string putContentType = webRequestHeaders.HeaderContentType();
                        var putArguments = GenericPutArguments.Create(body, putContentType);
                        return await webController.PutAsync<GenerateResponseOp<GenericPutRequest>, ISimpleFetchApi.Response>(commonArguments, new GenerateResponseOp<GenericPutRequest>(), putArguments, ct, GetReportData(), webRequestHeaders);
                    case RequestMethod.DELETE:
                        string deleteContentType = webRequestHeaders.HeaderContentType();
                        var deleteArguments = GenericDeleteArguments.Create(body, deleteContentType);
                        return await webController.DeleteAsync<GenerateResponseOp<GenericDeleteRequest>, ISimpleFetchApi.Response>(commonArguments, new GenerateResponseOp<GenericDeleteRequest>(), deleteArguments, ct, GetReportData(), webRequestHeaders);
                    case RequestMethod.PATCH:
                        string patchContentType = webRequestHeaders.HeaderContentType();
                        var patchArguments = GenericPatchArguments.Create(body, patchContentType);
                        return await webController.PatchAsync<GenerateResponseOp<GenericPatchRequest>, ISimpleFetchApi.Response>(commonArguments, new GenerateResponseOp<GenericPatchRequest>(), patchArguments, ct, GetReportData(), webRequestHeaders);
                    case RequestMethod.HEAD: throw new NotImplementedException();
                    case RequestMethod.INVALID:
                    default: throw new ArgumentOutOfRangeException();
                }
            }
            catch (UnityWebRequestException e)
            {
                return new ISimpleFetchApi.Response
                {
                    Ok = false,
                    Status = (int)e.ResponseCode,
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

        private struct GenerateResponseOp<TGenericRequest> : IWebRequestOp<TGenericRequest, ISimpleFetchApi.Response>
            where TGenericRequest: struct, GenericDownloadHandlerUtils.IGenericDownloadHandlerRequest, ITypedWebRequest
        {
            public async UniTask<ISimpleFetchApi.Response> ExecuteAsync(TGenericRequest request, CancellationToken ct)
            {
                UnityWebRequest unityWebRequest = request.UnityWebRequest;
                string responseData = unityWebRequest.downloadHandler?.text ?? string.Empty;
                Dictionary<string, string>? responseHeadersDictionary = unityWebRequest.GetResponseHeaders();
                bool requestOk = unityWebRequest.result == UnityWebRequest.Result.Success;
                bool requestRedirected = unityWebRequest.result is UnityWebRequest.Result.ProtocolError or UnityWebRequest.Result.ConnectionError;
                var requestStatus = (int)unityWebRequest.responseCode;
                var requestStatusText = unityWebRequest.responseCode.ToString();
                string requestUrl = unityWebRequest.url.EnsureNotNull();

                var result = new ISimpleFetchApi.Response
                {
                    Headers = responseHeadersDictionary,
                    Ok = requestOk,
                    Redirected = requestRedirected,
                    Status = requestStatus,
                    StatusText = requestStatusText,
                    URL = requestUrl,
                    Data = responseData,
                    Type = "basic", //Handle Response Types properly  type ResponseType = 'basic' | 'cors' | 'default' | 'error' | 'opaque' | 'opaqueredirect'
                };

                return result;
            }
        }
    }
}
