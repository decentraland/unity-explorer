using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Optimization.Pools;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using SceneRunner.Scene;
using System.Threading;
using Utility;

namespace ECS.StreamableLoading.AssetBundles
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.ASSET_BUNDLES)]
    public partial class LoadAssetBundleManifestSystem : LoadSystemBase<SceneAssetBundleManifest, GetAssetBundleManifestIntention>
    {
        private readonly URLBuilder urlBuilder = new ();
        private readonly URLDomain assetBundleURL;
        private readonly IWebRequestController webRequestController;

        internal LoadAssetBundleManifestSystem(World world,
            IStreamableCache<SceneAssetBundleManifest, GetAssetBundleManifestIntention> cache, URLDomain assetBundleURL, IWebRequestController webRequestController) : base(world, cache)
        {
            this.assetBundleURL = assetBundleURL;
            this.webRequestController = webRequestController;
        }

        protected override async UniTask<StreamableLoadingResult<SceneAssetBundleManifest>> FlowInternalAsync(GetAssetBundleManifestIntention intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct) =>
            new (
                await LoadAssetBundleManifestAsync(
                    webRequestController,
                    assetBundleURL,
                    intention.Hash,
                    GetReportData(),
                    ct
                )
            );

        private async UniTask<SceneAssetBundleManifest> LoadAssetBundleManifestAsync(IWebRequestController webRequestController, URLDomain assetBundleURL,
            string hash, ReportData reportCategory, CancellationToken ct)
        {
            urlBuilder!.Clear();

            urlBuilder.AppendDomain(assetBundleURL)
                      .AppendSubDirectory(URLSubdirectory.FromString("manifest"))
                      .AppendPath(URLPath.FromString($"{hash}{PlatformUtils.GetCurrentPlatform()}.json"));

            SceneAbDto sceneAbDto = await webRequestController.GetAsync(new CommonArguments(urlBuilder.Build(), attemptsCount: 1), ct, reportCategory)
                                                              .CreateFromJson<SceneAbDto>(WRJsonParser.Unity, WRThreadFlags.SwitchBackToMainThread);

            AssetValidation.ValidateSceneAbDto(sceneAbDto, AssetValidation.WearableIDError, hash);

            return new SceneAssetBundleManifest(assetBundleURL, sceneAbDto.Version, sceneAbDto.Files, hash, sceneAbDto.Date);
        }
    }
}
