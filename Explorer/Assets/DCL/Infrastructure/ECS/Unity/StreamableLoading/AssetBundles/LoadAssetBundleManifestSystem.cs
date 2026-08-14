using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Optimization.Pools;
using DCL.Platforms;
using DCL.Utility;
using DCL.WebRequests;
using ECS.Groups;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace ECS.StreamableLoading.AssetBundles
{
    [UpdateInGroup(typeof(LoadGlobalSystemGroup))]
    [LogCategory(ReportCategory.ASSET_BUNDLES)]
    public partial class LoadAssetBundleManifestSystem : LoadSystemBase<SceneAssetBundleManifest, GetAssetBundleManifestIntention>
    {
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
            SceneAssetBundleManifest sceneAssetBundleManifest =
                    await LoadAssetBundleManifestAsync(
                        intention.Hash,
                        GetReportData(),
                        ct
                    );

            return new StreamableLoadingResult<SceneAssetBundleManifest>(sceneAssetBundleManifest);
        }


        private async UniTask<SceneAssetBundleManifest> LoadAssetBundleManifestAsync(string hash, ReportData reportCategory, CancellationToken ct)
        {
            using PooledObject<URLBuilder> scope = DecentralandUrlsUtils.BuildFromDomain(assetBundleURL, out URLBuilder urlBuilder);

            urlBuilder.AppendSubDirectory(URLSubdirectory.FromString("manifest"))
                      .AppendPath(URLPath.FromString($"{hash}{PlatformUtils.GetCurrentPlatform()}.json"));

            URLAddress url = urlBuilder.Build();

            // In local-ab the abgen sidecar already fetched this exact manifest — boot holds on the
            // warm-up's request, and the reconversion watcher re-fetches after every content edit — so
            // reuse the held response instead of paying the server's content revalidation a second time
            // on the scene-entry critical path. Empty outside that flow.
            SceneAbDto sceneAbDto;

            if (AbgenManifestPrewarm.TryGet(url.Value, out string prewarmedJson))
            {
                sceneAbDto = JsonUtility.FromJson<SceneAbDto>(prewarmedJson);
                ReportHub.Log(ReportCategory.ASSET_BUNDLES, $"manifest for {hash} served from the abgen warm-up hand-off (no re-fetch)");
            }
            else
                sceneAbDto = await webRequestController.GetAsync(new CommonArguments(url, RetryPolicy.WithRetries(1)), ct, reportCategory)
                                                       .CreateFromJson<SceneAbDto>(WRJsonParser.Newtonsoft, WRThreadFlags.SwitchBackToMainThread);

            CheckSceneAbDTO(sceneAbDto.Version, hash);

            return new SceneAssetBundleManifest(sceneAbDto.Version, sceneAbDto.Date, sceneAbDto.files);
        }


        private void CheckSceneAbDTO(string version, string hash)
        {
            if (string.IsNullOrEmpty(version))
                ReportHub.LogError(ReportCategory.ASSET_BUNDLES, $"Asset bundle version missing for {hash}");

            var intVersion = int.Parse(version.AsSpan().Slice(1));
            int supportedVersion = IPlatform.DEFAULT.Is(IPlatform.Kind.Windows) ? AssetBundleManifestVersion.AB_MIN_SUPPORTED_VERSION_WINDOWS : AssetBundleManifestVersion.AB_MIN_SUPPORTED_VERSION_MAC;

            if (intVersion < supportedVersion)
                ReportHub.LogError(ReportCategory.ASSET_BUNDLES, $"Asset bundle version {intVersion} is not supported. Minimum supported version is {supportedVersion}, Asset bundle {hash} requires rebuild");
        }
    }
}
