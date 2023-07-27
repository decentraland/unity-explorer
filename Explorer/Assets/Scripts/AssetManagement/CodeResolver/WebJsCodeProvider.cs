using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine.Networking;

namespace AssetManagement.JsCodeResolver
{
    public class WebJsCodeProvider : IJsCodeProvider
    {
        public async UniTask<string> GetJsCodeAsync(string url, CancellationToken cancellationToken = default)
        {
            using var request = UnityWebRequest.Get(url);

            await request.SendWebRequest().WithCancellation(cancellationToken);

            return request.downloadHandler.text;
        }
    }
}
