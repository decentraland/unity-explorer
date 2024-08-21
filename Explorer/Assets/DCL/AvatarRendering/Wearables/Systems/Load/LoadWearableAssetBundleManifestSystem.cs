using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Thumbnails.Utils;
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

namespace DCL.AvatarRendering.Wearables.Systems.Load
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.ASSET_BUNDLES)]
    public partial class LoadWearableAssetBundleManifestSystem : LoadSystemBase<SceneAssetBundleManifest, GetWearableAssetBundleManifestIntention>
    {
        private readonly URLDomain assetBundleURL;
        private readonly IWebRequestController webRequestController;

        internal LoadWearableAssetBundleManifestSystem(World world,
            IStreamableCache<SceneAssetBundleManifest, GetWearableAssetBundleManifestIntention> cache, URLDomain assetBundleURL, IWebRequestController webRequestController) : base(world, cache)
        {
            this.assetBundleURL = assetBundleURL;
            this.webRequestController = webRequestController;
        }

        protected override async UniTask<StreamableLoadingResult<SceneAssetBundleManifest>> FlowInternalAsync(GetWearableAssetBundleManifestIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            return new StreamableLoadingResult<SceneAssetBundleManifest>(
                await LoadThumbnailsUtils.LoadAssetBundleManifestAsync(webRequestController, assetBundleURL, intention.Hash, GetReportCategory(), ct));
        }
    }
}
