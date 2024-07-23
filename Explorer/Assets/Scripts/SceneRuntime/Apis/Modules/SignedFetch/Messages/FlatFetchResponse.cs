using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using UnityEngine.Networking;
using PropertyBag = Microsoft.ClearScript.PropertyBag;

namespace SceneRuntime.Apis.Modules.SignedFetch.Messages
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public struct FlatFetchResponse
    {
        public readonly bool ok;
        public readonly long status;
        public readonly string statusText;
        public readonly string body;
        public readonly PropertyBag headers;

        public FlatFetchResponse(bool ok, long status, string statusText, string body, Dictionary<string, string> headers)
        {
            this.ok = ok;
            this.status = status;
            this.statusText = statusText;
            this.body = body;
            this.headers = new PropertyBag();

            foreach (var header in headers)
                this.headers.Add(header.Key, header.Value);
        }
    }

    public struct FlatFetchResponse<TRequest> : IWebRequestOp<TRequest, FlatFetchResponse> where TRequest : struct, ITypedWebRequest
    {
        public UniTask<FlatFetchResponse> ExecuteAsync(TRequest request, CancellationToken ct)
        {
            var webRequest = request.UnityWebRequest;

            return UniTask.FromResult(new FlatFetchResponse(
                webRequest.result is UnityWebRequest.Result.Success,
                webRequest.responseCode,
                webRequest.responseCode.ToString()!,
                webRequest.downloadHandler?.text ?? string.Empty,
                webRequest.GetResponseHeaders()!
            ));
        }
    }
}
