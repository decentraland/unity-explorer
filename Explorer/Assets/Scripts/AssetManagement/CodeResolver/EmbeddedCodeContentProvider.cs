using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace AssetManagement.CodeResolver
{
    public class EmbeddedCodeContentProvider : ICodeContentProvider
    {
        public UniTask<string> GetCodeAsync(string url, CancellationToken cancellationToken = default) =>
            RequestFile(GetStreamingAssetURL(url), cancellationToken);

        async UniTask<string> RequestFile(string path, CancellationToken cancellationToken)
        {
            var request = UnityWebRequest.Get(path);

            await request.SendWebRequest().WithCancellation(cancellationToken);

            if (request.result == UnityWebRequest.Result.Success)
                return request.downloadHandler.text;

            throw new Exception($"Asset request failed with error {request.error}");
        }

        private static string GetStreamingAssetURL(string contentURL)
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            return $"file://{Application.streamingAssetsPath}/{contentURL}";
#endif
            return $"{Application.streamingAssetsPath}/{contentURL}";
        }
    }
}
