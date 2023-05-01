using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine.Networking;

namespace AssetManagement.JsCodeResolver
{
    public class WebJsCodeProvider : IJsCodeProvider
    {
        public async UniTask<string> GetJsCodeAsync(string url, CancellationToken cancellationToken = default)
        {
            var request = UnityWebRequest.Get(url);

            await request.SendWebRequest().WithCancellation(cancellationToken);

            if (request.result == UnityWebRequest.Result.Success)
                return request.downloadHandler.text;
            else
                throw new Exception($"Asset request failed with error {request.error}");
        }
    }
}
