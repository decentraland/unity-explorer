using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using SceneRunner.Scene;
using System.Threading;
using Utility;

namespace DCL.AvatarRendering.Wearables.Systems.Load
{
    public static class LoadWearableAssetBundleManifestUtils
    {
        private static readonly URLBuilder URL_BUILDER = new ();

        public static async UniTask<SceneAssetBundleManifest> LoadWearableAssetBundleManifestAsync(IWebRequestController webRequestController, URLDomain assetBundleURL,
            string hash, string reportCategory, CancellationToken ct)
        {
            URL_BUILDER.Clear();

            URL_BUILDER.AppendDomain(assetBundleURL)
                .AppendSubDirectory(URLSubdirectory.FromString("manifest"))
                .AppendPath(URLPath.FromString($"{hash}{PlatformUtils.GetCurrentPlatform()}.json"));

            var sceneAbDto = await webRequestController.GetAsync(new CommonArguments(URL_BUILDER.Build(), attemptsCount: 1), ct, reportCategory)
                .CreateFromJson<SceneAbDto>(WRJsonParser.Unity, WRThreadFlags.SwitchBackToMainThread);

            return new SceneAssetBundleManifest(assetBundleURL, sceneAbDto.Version, sceneAbDto.Files);
        }
    }
}
