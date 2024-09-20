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

        internal static GetAssetBundleWebRequest Initialize(in CommonArguments commonArguments, GetAssetBundleArguments arguments)
        {
            UnityWebRequest unityWebRequest =
                arguments.CacheHash.HasValue
                    ? UnityWebRequestAssetBundle.GetAssetBundle(commonArguments.URL, arguments.CacheHash.Value)
                    : UnityWebRequestAssetBundle.GetAssetBundle(commonArguments.URL);

            ((DownloadHandlerAssetBundle)unityWebRequest.downloadHandler).autoLoadAssetBundle = arguments.AutoLoadAssetBundle;

            return new GetAssetBundleWebRequest(unityWebRequest, arguments.LoadingMutex);
        }

        public struct CreateAssetBundleOp : IWebRequestOp<GetAssetBundleWebRequest, AssetBundleLoadingResult>
        {
            public async UniTask<AssetBundleLoadingResult> ExecuteAsync(GetAssetBundleWebRequest webRequest, CancellationToken ct)
            {
                AssetBundle assetBundle;

                using (AssetBundleLoadingMutex.LoadingRegion _ = await webRequest.assetBundleLoadingMutex.AcquireAsync(ct))
                    assetBundle = DownloadHandlerAssetBundle.GetContent(webRequest.UnityWebRequest);

                string? error = assetBundle == null ? webRequest.UnityWebRequest.downloadHandler.error : null;
                return new AssetBundleLoadingResult(assetBundle, error);
            }
        }
    }
}
