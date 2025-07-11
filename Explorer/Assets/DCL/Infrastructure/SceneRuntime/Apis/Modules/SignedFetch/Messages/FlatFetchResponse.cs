using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using UnityEngine.Networking;
using PropertyBag = Microsoft.ClearScript.PropertyBag;

namespace SceneRuntime.Apis.Modules.SignedFetch.Messages
{
    public struct FlatFetchError
    {
        public bool ok;
        public string error;
        public string code;
    }

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

    public static class FlatFetchExtensions
    {
        public static async UniTask<FlatFetchResponse> ToFlatFetchResponseAsync(this ITypedWebRequest request, CancellationToken ct)
        {
            using IWebRequest? sentRequest = await request.SendAsync(ct);

            return new FlatFetchResponse(sentRequest.Response.IsSuccess,
                sentRequest.Response.StatusCode,
                sentRequest.Response.StatusCode.ToString(),
                await sentRequest.Response.GetTextAsync(ct),
                sentRequest.Response.FlattenHeaders()!);
        }
    }
}
