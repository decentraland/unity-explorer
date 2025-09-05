using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using DCL.Platforms;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace ECS.StreamableLoading.AssetBundles
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.ASSET_BUNDLES)]
    public partial class LoadAssetBundleManifestSystem : LoadSystemBase<SceneAssetBundleManifest, GetAssetBundleManifestIntention>
    {
        private static readonly IExtendedObjectPool<URLBuilder> URL_BUILDER_POOL = new ExtendedObjectPool<URLBuilder>(() => new URLBuilder(), defaultCapacity: 2);
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
            ReportHub.Log(ReportCategory.ALWAYS, $"JUANI STARTING REQUEST OF SCENE ASSET BUNDLE MANIFEST {intention.Hash}");

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
            using var scope = URL_BUILDER_POOL.Get(out var urlBuilder);
            urlBuilder!.Clear();

            urlBuilder.AppendDomain(assetBundleURL)
                      .AppendSubDirectory(URLSubdirectory.FromString("manifest"))
                      .AppendPath(URLPath.FromString($"{hash}{PlatformUtils.GetCurrentPlatform()}.json"));

            URLAddress url = urlBuilder.Build();
            ReportHub.Log(ReportCategory.ALWAYS, $"JUANI BUILDING URL {url}");

            SceneAbDto sceneAbDto = await webRequestController.GetAsync(new CommonArguments(url, RetryPolicy.WithRetries(1)), ct, reportCategory)
                                                              .CreateFromJson<SceneAbDto>(WRJsonParser.Newtonsoft, WRThreadFlags.SwitchBackToMainThread);

            CheckSceneAbDTO(sceneAbDto.Version, hash);

            return new SceneAssetBundleManifest(sceneAbDto.Version, sceneAbDto.Date);
        }


        private const int AB_MIN_SUPPORTED_VERSION_WINDOWS = 15;
        private const int AB_MIN_SUPPORTED_VERSION_MAC = 16;

        private void CheckSceneAbDTO(string version, string hash)
        {
            if (string.IsNullOrEmpty(version))
                ReportHub.LogError(ReportCategory.ASSET_BUNDLES, $"Asset bundle version missing for {hash}");

            var intVersion = int.Parse(version.AsSpan().Slice(1));
            int supportedVersion  = IPlatform.DEFAULT.Is(IPlatform.Kind.Windows) ? AB_MIN_SUPPORTED_VERSION_WINDOWS : AB_MIN_SUPPORTED_VERSION_MAC;

            if (intVersion < supportedVersion)
                ReportHub.LogError(ReportCategory.ASSET_BUNDLES, $"Asset bundle version {intVersion} is not supported. Minimum supported version is {supportedVersion}, Asset bundle {hash} requires rebuild");
        }
    }
}
