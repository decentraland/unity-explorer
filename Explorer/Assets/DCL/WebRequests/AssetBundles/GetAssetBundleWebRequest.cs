using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public readonly struct GetAssetBundleWebRequest : ITypedWebRequest
    {
        private readonly AssetBundleLoadingMutex assetBundleLoadingMutex;

        public GetAssetBundleWebRequest(UnityWebRequest unityWebRequest, AssetBundleLoadingMutex assetBundleLoadingMutex)
        {
            this.assetBundleLoadingMutex = assetBundleLoadingMutex;
            UnityWebRequest = unityWebRequest;
        }

        public UnityWebRequest UnityWebRequest { get; }

        public bool Idempotent => true;

        internal static GetAssetBundleWebRequest Initialize(string url, GetAssetBundleArguments arguments, bool disableABCache)
        {
            UnityWebRequest unityWebRequest =
                arguments.CacheHash.HasValue && !disableABCache
                    ? UnityWebRequestAssetBundle.GetAssetBundle(url, arguments.CacheHash.Value)
                    : UnityWebRequestAssetBundle.GetAssetBundle(url);

            ((DownloadHandlerAssetBundle)unityWebRequest.downloadHandler).autoLoadAssetBundle = arguments.AutoLoadAssetBundle;

            return new GetAssetBundleWebRequest(unityWebRequest, arguments.LoadingMutex);
        }

        public struct CreateAssetBundleOp : IWebRequestOp<GetAssetBundleWebRequest, AssetBundleLoadingResult>
        {
            public async UniTask<AssetBundleLoadingResult> ExecuteAsync(GetAssetBundleWebRequest webRequest, CancellationToken ct)
            {
                ulong downloadedBytes = webRequest.UnityWebRequest.downloadedBytes;

                // GetResponseHeader returns null when the header is absent; TryParse handles null safely
                string contentLengthHeader = webRequest.UnityWebRequest.GetResponseHeader("Content-Length");
                long contentLength = long.TryParse(contentLengthHeader, out long cl) ? cl : -1;

                AssetBundle assetBundle;

                using (AssetBundleLoadingMutex.LoadingRegion _ = await webRequest.assetBundleLoadingMutex.AcquireAsync(ct))
                    assetBundle = DownloadHandlerAssetBundle.GetContent(webRequest.UnityWebRequest);

                string? error = assetBundle == null ? webRequest.UnityWebRequest.downloadHandler.error : null;
                return new AssetBundleLoadingResult(assetBundle, error, downloadedBytes, contentLength);
            }
        }
    }
}
