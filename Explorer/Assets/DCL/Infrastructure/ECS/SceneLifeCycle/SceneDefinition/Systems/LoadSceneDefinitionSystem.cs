using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using System.Threading;

namespace ECS.SceneLifeCycle.SceneDefinition
{
    /// <summary>
    ///     Loads a single scene definition from URN
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.SCENE_LOADING)]
    public partial class LoadSceneDefinitionSystem : LoadSystemBase<SceneEntityDefinition, GetSceneDefinition>
    {
        private readonly IWebRequestController webRequestController;
        private readonly URLBuilder urlBuilder = new ();
        private readonly URLDomain assetBundleURL;



        internal LoadSceneDefinitionSystem(World world, IWebRequestController webRequestController, IStreamableCache<SceneEntityDefinition, GetSceneDefinition> cache, URLDomain assetBundleURL)
            : base(world, cache)
        {
            this.webRequestController = webRequestController;
            this.assetBundleURL = assetBundleURL;
        }

        protected override async UniTask<StreamableLoadingResult<SceneEntityDefinition>> FlowInternalAsync(GetSceneDefinition intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            SceneEntityDefinition sceneEntityDefinition = await
                webRequestController.GetAsync(intention.CommonArguments, ct, GetReportData())
                                    .CreateFromJson<SceneEntityDefinition>(WRJsonParser.Newtonsoft, WRThreadFlags.SwitchToThreadPool);

            sceneEntityDefinition.id ??= intention.IpfsPath.EntityId;


            //Fallback needed for when the asset-bundle-registry does not have the asset bundle manifest.
            //Could be removed once the asset bundle manifest registry has been battle tested
            await AssetBundleManifestFallbackHelper.CheckAssetBundleManifestFallback(World, sceneEntityDefinition, partition, ct);

            // switching back is handled by the base class
            return new StreamableLoadingResult<SceneEntityDefinition>(sceneEntityDefinition);
        }

    }
}
