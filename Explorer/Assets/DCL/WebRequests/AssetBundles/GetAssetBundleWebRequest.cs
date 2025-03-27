using Cysharp.Threading.Tasks;
using NSubstitute;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public class GetAssetBundleWebRequest : TypedWebRequestBase<GetAssetBundleArguments>
    {
        private readonly AssetBundleLoadingMutex assetBundleLoadingMutex;

        internal GetAssetBundleWebRequest(AssetBundleLoadingMutex assetBundleLoadingMutex, RequestEnvelope envelope, GetAssetBundleArguments args, IWebRequestController controller) : base(envelope, args, controller)
        {
            this.assetBundleLoadingMutex = assetBundleLoadingMutex;
        }

        public override UnityWebRequest CreateUnityWebRequest()
        {
            UnityWebRequest unityWebRequest =
                Args.CacheHash.HasValue
                    ? UnityWebRequestAssetBundle.GetAssetBundle(Envelope.CommonArguments.URL, Args.CacheHash.Value)
                    : UnityWebRequestAssetBundle.GetAssetBundle(Envelope.CommonArguments.URL);

            ((DownloadHandlerAssetBundle)unityWebRequest.downloadHandler).autoLoadAssetBundle = Args.AutoLoadAssetBundle;

            return unityWebRequest;
        }

        public async UniTask<AssetBundleLoadingResult> CreateAsyncBundleAsync(CancellationToken ct)
        {
            using IWebRequest? wr = await this.SendAsync(ct);

            if (wr.nativeRequest is not UnityWebRequest unityWebRequest)
                throw new NotSupportedException($"{nameof(CreateAsyncBundleAsync)} supports {nameof(UnityWebRequest)} only");

            AssetBundle assetBundle;

            using (AssetBundleLoadingMutex.LoadingRegion _ = await assetBundleLoadingMutex.AcquireAsync(ct))
                assetBundle = DownloadHandlerAssetBundle.GetContent(unityWebRequest);

            string? error = assetBundle == null ? unityWebRequest.downloadHandler.error : null;
            return new AssetBundleLoadingResult(assetBundle, error);
        }
    }
}
