using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Nethereum.Contracts;
using SceneRuntime.Apis;
using SceneRuntime.Apis.Modules;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace CrdtEcsBridge.Engine
{
    public class SimpleFetchApiImplementation : ISimpleFetchApi
    {
        private enum RequestMethod
        {
            GET,
            POST,
            PUT,
            PATCH,
            HEAD,
            INVALID,
        }

        private readonly Dictionary<string, string> headersDictionary = new ();

        public void Dispose() { }

        public async UniTask<ISimpleFetchApi.FetchResponse> Fetch(string requestMethod, string url, object headers, bool hasBody, string body,
            string redirect, int timeout, IWebRequestController webController, CancellationToken ct)
        {

            RequestMethod parsedRequestMethod = ParseRequestMethod(requestMethod);

            if (parsedRequestMethod == RequestMethod.INVALID) { throw new ArgumentException("Invalid request method."); }

            var commonArguments = new CommonArguments(URLAddress.FromString(url), timeout: timeout);
            var webRequestHeaders = new WebRequestHeadersInfo();
            headersDictionary.Clear();

            if (headers is IScriptObject scriptObject)
            {
                IEnumerable<string> propertyNames = scriptObject.PropertyNames;

                foreach (string name in propertyNames)
                {
                    var property = scriptObject.GetProperty(name).ToString();
                    webRequestHeaders.Add(name, property);
                    headersDictionary.Add(name, property);
                }
            }

            await UniTask.SwitchToMainThread();

            switch (parsedRequestMethod)
            {
                case RequestMethod.GET:
                    var getRequest = await webController.GetAsync(commonArguments, ct, ReportCategory.SCENE_FETCH_REQUEST, webRequestHeaders);
                    return GenerateResponse(getRequest);
                case RequestMethod.POST:
                    headersDictionary.TryGetValue("content-type", out string postContentType);
                    var postArguments = GenericPostArguments.Create(body, postContentType ?? "");
                    var postRequest = await webController.PostAsync(commonArguments, postArguments, ct, ReportCategory.SCENE_FETCH_REQUEST, webRequestHeaders);
                    return GenerateResponse(postRequest);
                case RequestMethod.PUT:
                    headersDictionary.TryGetValue("content-type", out string putContentType);
                    var putArguments = GenericPutArguments.Create(body, putContentType ?? "");
                    var putRequest = await webController.PutAsync(commonArguments, putArguments, ct, ReportCategory.SCENE_FETCH_REQUEST, webRequestHeaders);
                    return GenerateResponse(putRequest);
                case RequestMethod.PATCH:
                    headersDictionary.TryGetValue("content-type", out string patchContentType);
                    var patchArguments = GenericPatchArguments.Create(body, patchContentType ?? "");
                    var patchRequest = await webController.PatchAsync(commonArguments, patchArguments, ct, ReportCategory.SCENE_FETCH_REQUEST, webRequestHeaders);
                    return GenerateResponse(patchRequest);
                case RequestMethod.INVALID:
                default: throw new ArgumentOutOfRangeException();
            }
        }

        private ISimpleFetchApi.FetchResponse GenerateResponse<T>(T request) where T : ITypedWebRequest
        {
            UnityWebRequest unityWebRequest = request.UnityWebRequest;
            string responseData = unityWebRequest.downloadHandler?.text ?? string.Empty;

            var headers = new Dictionary<string, string>();

            foreach (string headerKey in unityWebRequest.GetResponseHeaders().Keys)
            {
                string headerValue = unityWebRequest.GetResponseHeader(headerKey);
                headers.Add(headerKey, headerValue);
            }

            bool ok = unityWebRequest.result == UnityWebRequest.Result.Success;
            bool redirected = unityWebRequest.result == UnityWebRequest.Result.ProtocolError || unityWebRequest.result == UnityWebRequest.Result.ConnectionError;
            var status = (int)unityWebRequest.responseCode;
            var statusText = unityWebRequest.responseCode.ToString();
            string url = unityWebRequest.url;

            unityWebRequest.Dispose();
            return new ISimpleFetchApi.FetchResponse(headers, ok, redirected, status, statusText, url, responseData);
        }

        private RequestMethod ParseRequestMethod(string request) =>
            Enum.TryParse(request, true, out RequestMethod method) ? method : RequestMethod.INVALID;
    }
}
