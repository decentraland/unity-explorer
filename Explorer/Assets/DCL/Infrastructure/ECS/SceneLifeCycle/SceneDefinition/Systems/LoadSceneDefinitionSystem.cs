using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using SceneRunner.Scene;
using System;
using System.Threading;
using Utility;


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

            try
            {
                //TODO (JUANI) : Remove after it comes with the asset-bundle-registry
                SceneAssetBundleManifest sceneAssetBundleManifest =
                    await LoadAssetBundleManifestAsync(
                        sceneEntityDefinition.id,
                        GetReportData(),
                        ct
                    );

                sceneEntityDefinition.assetBundleManifestVersion = sceneAssetBundleManifest.GetVersion();
                sceneEntityDefinition.hasSceneInPath = sceneAssetBundleManifest.HasHashInPathID();
            }
            catch (Exception e)
            {
                sceneEntityDefinition.assetBundleManifestRequestFailed = true;
            }

            // switching back is handled by the base class
            return new StreamableLoadingResult<SceneEntityDefinition>(sceneEntityDefinition);
        }


        private async UniTask<SceneAssetBundleManifest> LoadAssetBundleManifestAsync(string hash, ReportData reportCategory, CancellationToken ct)
        {
            urlBuilder!.Clear();

            urlBuilder.AppendDomain(assetBundleURL)
                      .AppendSubDirectory(URLSubdirectory.FromString("manifest"))
                      .AppendPath(URLPath.FromString($"{hash}{PlatformUtils.GetCurrentPlatform()}.json"));

            SceneAbDto sceneAbDto = await webRequestController.GetAsync(new CommonArguments(urlBuilder.Build(), RetryPolicy.WithRetries(1)), ct, reportCategory)
                                                              .CreateFromJson<SceneAbDto>(WRJsonParser.Unity, WRThreadFlags.SwitchBackToMainThread);

            AssetValidation.ValidateSceneAbDto(sceneAbDto, AssetValidation.WearableIDError, hash);

            return new SceneAssetBundleManifest(sceneAbDto.Version);
        }
    }
}
