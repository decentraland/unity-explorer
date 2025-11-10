using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using DCL.WebRequests;
using ECS.Groups;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ECS.StreamableLoading.AssetBundles
{
    [UpdateInGroup(typeof(LoadGlobalSystemGroup))]
    [LogCategory(ReportCategory.ASSET_BUNDLES)]
    public partial class LoadAssetBundleRegistryVersionsSystem : LoadSystemBase<AssetBundlesVersions, GetAssetBundleRegistryVersionsIntention>
    {
        private static readonly IExtendedObjectPool<URLBuilder> URL_BUILDER_POOL = new ExtendedObjectPool<URLBuilder>(() => new URLBuilder(), defaultCapacity: 2);
        private static readonly ThreadSafeListPool<ABVersionsResponse> DTO_POOL = new (25, 50);

        private readonly URLDomain assetBundleRegistryVersionURL;
        private readonly IWebRequestController webRequestController;
        private readonly StringBuilder bodyBuilder = new ();

        internal LoadAssetBundleRegistryVersionsSystem(World world, IStreamableCache<AssetBundlesVersions, GetAssetBundleRegistryVersionsIntention> cache,
            URLDomain assetBundleRegistryVersionURL, IWebRequestController webRequestController) : base(world, cache)
        {
            this.assetBundleRegistryVersionURL = assetBundleRegistryVersionURL;
            this.webRequestController = webRequestController;
        }

        protected override async UniTask<StreamableLoadingResult<AssetBundlesVersions>> FlowInternalAsync(GetAssetBundleRegistryVersionsIntention intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            AssetBundlesVersions assetBundlesVersions =
                await LoadAssetBundlesVersionsAsync(
                    intention.Pointers,
                    GetReportData(),
                    ct
                );

            return new StreamableLoadingResult<AssetBundlesVersions>(assetBundlesVersions);
        }

        private async UniTask<AssetBundlesVersions> LoadAssetBundlesVersionsAsync(URN[] pointers, ReportData reportCategory, CancellationToken ct)
        {
            await UniTask.SwitchToMainThread();

            using var scope = URL_BUILDER_POOL.Get(out var urlBuilder);
            urlBuilder!.Clear();

            urlBuilder.AppendDomain(assetBundleRegistryVersionURL);

            bodyBuilder.Clear();
            bodyBuilder.Append("{\"pointers\":[");

            for (int i = 0; i < pointers.Length; ++i)
            {
                bodyBuilder.Append('\"');

                bodyBuilder.Append(pointers[i].LowerCaseUrn());
                bodyBuilder.Append('\"');

                if (i != pointers.Length - 1)
                    bodyBuilder.Append(",");
            }

            bodyBuilder.Append("]}");

            URLAddress url = urlBuilder.Build();
            using PoolExtensions.Scope<List<ABVersionsResponse>> dtoPooledList = DTO_POOL.AutoScope();

            await webRequestController.PostAsync(new CommonArguments(url), GenericPostArguments.CreateJson(bodyBuilder.ToString()), ct, reportCategory)
                                      .OverwriteFromJsonAsync(dtoPooledList.Value, WRJsonParser.Newtonsoft, WRThreadFlags.SwitchToThreadPool);

            AssetBundlesVersions result = AssetBundlesVersions.Create();

            foreach (var element in dtoPooledList.Value)
                result.versions.Add(element.pointers[0], new AssetBundlesVersions.PlatformVersionInfo
                {
                    mac = new AssetBundlesVersions.VersionInfo
                    {
                        version = element.versions.assets.mac.version,
                        buildDate = element.versions.assets.mac.buildDate,
                    },
                    windows = new AssetBundlesVersions.VersionInfo
                    {
                        version = element.versions.assets.windows.version,
                        buildDate = element.versions.assets.windows.buildDate,
                    }
                });

            return result;
        }
    }
}
