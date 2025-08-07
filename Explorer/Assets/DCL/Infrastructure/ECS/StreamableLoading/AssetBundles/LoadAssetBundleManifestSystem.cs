using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using SceneRunner.Scene;
using System;
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

        //TODO (JUANI): This whole system can go away once the information comes the entity DTO
        internal LoadAssetBundleManifestSystem(World world,
            IStreamableCache<SceneAssetBundleManifest, GetAssetBundleManifestIntention> cache, URLDomain assetBundleURL, IWebRequestController webRequestController) : base(world, cache)
        {
            this.assetBundleURL = assetBundleURL;
            this.webRequestController = webRequestController;
        }

        protected override async UniTask<StreamableLoadingResult<SceneAssetBundleManifest>> FlowInternalAsync(GetAssetBundleManifestIntention intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            SceneAssetBundleManifest sceneAssetBundleManifest = null;
            try
            {
                sceneAssetBundleManifest =
                    await LoadAssetBundleManifestAsync(
                        intention.Hash,
                        GetReportData(),
                        ct
                    );

                //TODO (JUANI): When we have the manifest version embedded in the entity, we can delete all of this as the number will be already applied
                intention.ApplyAssetBundleManifestResultTo.ApplyAssetBundleManifestResult(sceneAssetBundleManifest.GetVersion(), sceneAssetBundleManifest.HasHashInPathID());
            }
            catch (Exception e)
            {
                //On exception, we can apply a failed result
                intention.ApplyAssetBundleManifestResultTo.ApplyFailedManifestResult();
            }

            //We do nothing with this result currently
            return new StreamableLoadingResult<SceneAssetBundleManifest>(sceneAssetBundleManifest);
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

            return new SceneAssetBundleManifest(sceneAbDto.Version, sceneAbDto.Date);
        }
    }
}
