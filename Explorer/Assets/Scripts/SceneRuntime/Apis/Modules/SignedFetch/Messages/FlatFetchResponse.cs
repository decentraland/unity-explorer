using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine.Networking;

namespace SceneRuntime.Apis.Modules.SignedFetch.Messages
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class FlatFetchResponse
    {
        public bool ok;
        public long status;
        public string statusText;
        public string body;
        public Dictionary<string, string> headers;

        public FlatFetchResponse(UnityWebRequest webRequest) : this(
            webRequest.result is UnityWebRequest.Result.Success,
            webRequest.responseCode,
            webRequest.responseCode.ToString()!,
            webRequest.downloadHandler?.text ?? string.Empty,
            webRequest.GetResponseHeaders()!
        ) { }

        public FlatFetchResponse(bool ok, long status, string statusText, string body, Dictionary<string, string> headers)
        {
            this.ok = ok;
            this.status = status;
            this.statusText = statusText;
            this.body = body;
            this.headers = headers;
        }

        public static async UniTask<FlatFetchResponse> NewAsync(UniTask<UnityWebRequest> task)
        {
            var result = await task;
            return new FlatFetchResponse(result);
        }
    }
}
