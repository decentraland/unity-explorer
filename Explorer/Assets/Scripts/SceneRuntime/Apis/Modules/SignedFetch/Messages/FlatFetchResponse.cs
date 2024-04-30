using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using UnityEngine.Networking;

namespace SceneRuntime.Apis.Modules.SignedFetch.Messages
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public struct FlatFetchResponse<TRequest> : IWebRequestOp<TRequest> where TRequest : struct, ITypedWebRequest
    {
        public bool ok;
        public long status;
        public string statusText;
        public string body;
        public Dictionary<string, string> headers;

        public UniTask ExecuteAsync(TRequest request, CancellationToken ct)
        {
            var webRequest = request.UnityWebRequest;

            ok = webRequest.result is UnityWebRequest.Result.Success;
            status = webRequest.responseCode;
            statusText = webRequest.responseCode.ToString()!;
            body = webRequest.downloadHandler?.text ?? string.Empty;
            headers = webRequest.GetResponseHeaders()!;

            return UniTask.CompletedTask;
        }
    }
}
