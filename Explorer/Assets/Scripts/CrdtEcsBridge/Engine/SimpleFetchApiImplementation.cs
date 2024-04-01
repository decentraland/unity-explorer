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

            ITypedWebRequest request;

            switch (parsedRequestMethod)
            {
                case RequestMethod.GET:
                    request = await webController.GetAsync(commonArguments, ct, ReportCategory.SCENE_FETCH_REQUEST, webRequestHeaders);
                    GenerateResponse(request);
                    break;
                case RequestMethod.POST:
                    headersDictionary.TryGetValue("content-type", out string postContentType);
                    var postArguments = GenericPostArguments.Create(body, postContentType ?? "");
                    request = await webController.PostAsync(commonArguments, postArguments, ct, ReportCategory.SCENE_FETCH_REQUEST, webRequestHeaders);
                    GenerateResponse(request);
                    break;
                case RequestMethod.PUT:
                    headersDictionary.TryGetValue("content-type", out string putContentType);
                    var putArguments = GenericPutArguments.Create(body, putContentType ?? "");
                    request = await webController.PutAsync(commonArguments, putArguments, ct, ReportCategory.SCENE_FETCH_REQUEST, webRequestHeaders);
                    GenerateResponse(request);
                    break;
                case RequestMethod.PATCH:
                    headersDictionary.TryGetValue("content-type", out string patchContentType);
                    var patchArguments = GenericPatchArguments.Create(body, patchContentType ?? "");
                    request = await webController.PatchAsync(commonArguments, patchArguments, ct, ReportCategory.SCENE_FETCH_REQUEST, webRequestHeaders);
                    GenerateResponse(request);
                    break;
                case RequestMethod.INVALID:
                default: throw new ArgumentOutOfRangeException();
            }

            return new ISimpleFetchApi.FetchResponse();
        }

        private ISimpleFetchApi.FetchResponse GenerateResponse(ITypedWebRequest request)
        {
            UnityWebRequest unityWebRequest = request.UnityWebRequest;
            string responseData = unityWebRequest.downloadHandler?.text ?? string.Empty;

            var headers = new List<ISimpleFetchApi.Header>();

            foreach (string headerKey in unityWebRequest.GetResponseHeaders().Keys)
            {
                string headerValue = unityWebRequest.GetResponseHeader(headerKey);
                headers.Add(new ISimpleFetchApi.Header(headerKey, headerValue));
            }

            bool ok = unityWebRequest.result == UnityWebRequest.Result.Success;
            bool redirected = unityWebRequest.result == UnityWebRequest.Result.ProtocolError || unityWebRequest.result == UnityWebRequest.Result.ConnectionError;
            var status = (int)unityWebRequest.responseCode;
            var statusText = unityWebRequest.responseCode.ToString();
            string url = unityWebRequest.url;

            return new ISimpleFetchApi.FetchResponse(headers, ok, redirected, status, statusText, url, responseData);
        }

        private RequestMethod ParseRequestMethod(string request)
        {
            string upperCaseRequest = request.ToUpper();

            return Enum.TryParse(upperCaseRequest, out RequestMethod method) ? method : RequestMethod.INVALID;
        }
    }
}
