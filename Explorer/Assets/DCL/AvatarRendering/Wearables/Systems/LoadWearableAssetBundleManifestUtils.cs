using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using DCL.WebRequests;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using Utility;

namespace DCL.AvatarRendering.Wearables.Systems
{
    public static class LoadWearableAssetBundleManifestUtils
    {
        private static readonly URLBuilder urlBuilder = new ();
        private static readonly IWebRequestController webRequestController = IWebRequestController.DEFAULT;

        public static async UniTask<SceneAssetBundleManifest> LoadWearableAssetBundleManifestAsync(URLDomain assetBundleURL, string hash, string reportCategory, CancellationToken ct)
        {
            urlBuilder.Clear();

            urlBuilder.AppendDomain(assetBundleURL)
                .AppendSubDirectory(URLSubdirectory.FromString("manifest"))
                .AppendPath(URLPath.FromString($"{hash}{PlatformUtils.GetPlatform()}.json"));

            var sceneAbDto = await webRequestController.GetAsync(new CommonArguments(urlBuilder.Build(), attemptsCount: 1), ct, reportCategory)
                .CreateFromJson<SceneAbDto>(WRJsonParser.Unity, WRThreadFlags.SwitchBackToMainThread);


            return new SceneAssetBundleManifest(assetBundleURL, sceneAbDto.Version, sceneAbDto.Files);
        }
    }
}