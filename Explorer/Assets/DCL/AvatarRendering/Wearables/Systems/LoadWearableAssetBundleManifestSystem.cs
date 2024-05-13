using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using SceneRunner.Scene;
using System.Threading;
using Utility;
using Utility.Multithreading;

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.ASSET_BUNDLES)]
    public partial class LoadWearableAssetBundleManifestSystem : LoadSystemBase<SceneAssetBundleManifest, GetWearableAssetBundleManifestIntention>
    {
        private readonly URLDomain assetBundleURL;

        private readonly URLBuilder urlBuilder = new ();

        private readonly IWebRequestController webRequestController;

        internal LoadWearableAssetBundleManifestSystem(World world,
            IWebRequestController webRequestController,
            IStreamableCache<SceneAssetBundleManifest, GetWearableAssetBundleManifestIntention> cache, MutexSync mutexSync, URLDomain assetBundleURL) : base(world, cache)
        {
            this.assetBundleURL = assetBundleURL;
            this.webRequestController = webRequestController;
        }

        protected override async UniTask<StreamableLoadingResult<SceneAssetBundleManifest>> FlowInternalAsync(GetWearableAssetBundleManifestIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            urlBuilder.Clear();

            urlBuilder.AppendDomain(assetBundleURL)
                      .AppendSubDirectory(URLSubdirectory.FromString("manifest"))
                      .AppendPath(URLPath.FromString($"{intention.Hash}{PlatformUtils.GetPlatform()}.json"));

            SceneAbDto sceneAbDto = await (webRequestController.GetAsync(new CommonArguments(urlBuilder.Build(), attemptsCount: 1), ct, GetReportCategory()))
               .CreateFromJson<SceneAbDto>(WRJsonParser.Unity, WRThreadFlags.SwitchToThreadPool);

            return new StreamableLoadingResult<SceneAssetBundleManifest>(new SceneAssetBundleManifest(assetBundleURL, sceneAbDto.Version, sceneAbDto.Files));
        }
    }
}
