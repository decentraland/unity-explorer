using Cysharp.Threading.Tasks;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using SceneRuntime.Apis.Modules.FetchApi;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Networking;

namespace CrdtEcsBridge.JsModulesImplementation
{
    internal static class GenerateSimpleFetchApiResponse
    {
        public static async UniTask<ISimpleFetchApi.Response> ToSimpleFetchResponseAsync(this ITypedWebRequest request, CancellationToken ct)
        {
            using IWebRequest? nativeRequest = await request.SendAsync(ct);

            string responseData = await nativeRequest.Response.GetTextAsync(ct);
            Dictionary<string, string>? responseHeadersDictionary = nativeRequest.Response.FlattenHeaders();
            bool requestOk = nativeRequest.Response.IsSuccess;
            bool requestRedirected = nativeRequest.Redirected;
            int requestStatus = nativeRequest.Response.StatusCode;
            var requestStatusText = nativeRequest.Response.StatusCode.ToString();
            string requestUrl = nativeRequest.Url.EnsureNotNull();

            return new ISimpleFetchApi.Response
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
        }
    }
}
