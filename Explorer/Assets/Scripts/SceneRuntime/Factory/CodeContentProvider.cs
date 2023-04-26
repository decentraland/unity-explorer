using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

public class CodeContentProvider
{
    public async UniTask<string> GetFileByStreamingAsset(string contentUrl, CancellationToken cancellationToken = new CancellationToken())
    {
        return await RequestFile( GetStreamingAssetURL(contentUrl), cancellationToken);
    }

    public async UniTask<string> GetFileByURL(string contentUrl, CancellationToken cancellationToken = new CancellationToken())
    {
        return await RequestFile( contentUrl, cancellationToken);
    }

    private async UniTask<string> RequestFile(string path, CancellationToken cancellationToken)
    {
        UnityWebRequest request = UnityWebRequest.Get(path);

        await request.SendWebRequest().WithCancellation(cancellationToken);

        if (request.result == UnityWebRequest.Result.Success)
            return request.downloadHandler.text;
        else
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
