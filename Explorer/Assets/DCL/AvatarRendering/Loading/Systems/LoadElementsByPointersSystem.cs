using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DCL.AvatarRendering.Loading.Systems.Abstract
{
    public abstract class LoadElementsByPointersSystem<TAsset, TIntention, TDTO> : LoadSystemBase<TAsset, TIntention>
        where TIntention: struct, IPointersLoadingIntention, IEquatable<TIntention>
        where TDTO: EntityDefinitionBase
    {
        // When the number of wearables to request is greater than MAX_WEARABLES_PER_REQUEST, we split the request into several smaller ones.
        // In this way we avoid to send a very long url string that would fail due to the web request size limitations.
        protected const int MAX_WEARABLES_PER_REQUEST = 200;

        protected static readonly ThreadSafeListPool<TDTO> DTO_POOL = new (MAX_WEARABLES_PER_REQUEST, 50);

        private readonly IWebRequestController webRequestController;
        private readonly EntitiesAnalytics entitiesAnalytics;
        private readonly IDecentralandUrlsSource urlsSource;
        private readonly StringBuilder bodyBuilder = new ();

        protected LoadElementsByPointersSystem(World world,
            IStreamableCache<TAsset, TIntention> cache,
            IWebRequestController webRequestController,
            EntitiesAnalytics entitiesAnalytics,
            IDecentralandUrlsSource urlsSource)
            : base(world, cache)
        {
            this.webRequestController = webRequestController;
            this.entitiesAnalytics = entitiesAnalytics;
            this.urlsSource = urlsSource;
        }

        protected sealed override async UniTask<StreamableLoadingResult<TAsset>> FlowInternalAsync(TIntention intention, StreamableLoadingState state,
            IPartitionComponent partition, CancellationToken ct)
        {
            var finalTargetList = RepoolableList<TDTO>.NewList();

            int numberOfPartialRequests = (intention.Pointers.Count + MAX_WEARABLES_PER_REQUEST - 1) / MAX_WEARABLES_PER_REQUEST;

            var pointer = 0;

            for (var i = 0; i < numberOfPartialRequests; i++)
            {
                int numberOfWearablesToRequest = Mathf.Min(intention.Pointers.Count - pointer, MAX_WEARABLES_PER_REQUEST);

                await DoPartialRequestAsync(intention.CommonArguments.URL, intention.Pointers,
                    pointer, pointer + numberOfWearablesToRequest, finalTargetList.List, partition, ct);

                pointer += numberOfWearablesToRequest;
            }

            return new StreamableLoadingResult<TAsset>(CreateAssetFromListOfDTOs(finalTargetList));
        }

        protected abstract TAsset CreateAssetFromListOfDTOs(RepoolableList<TDTO> list);

        private async UniTask DoPartialRequestAsync(URLAddress url,
            IReadOnlyList<URN> wearablesToRequest, int startIndex, int endIndex, List<TDTO> results, IPartitionComponent partitionComponent,
            CancellationToken ct)
        {
            await UniTask.SwitchToMainThread();

            AssetBundlesVersions abVersions = await AssetBundleRegistryVersionHelper.GetABRegistryVersionsByPointersAsync(wearablesToRequest, webRequestController, urlsSource.Url(DecentralandUrl.AssetBundleRegistryVersion), GetReportData(), ct);

            bodyBuilder.Clear();
            bodyBuilder.Append("{\"pointers\":[");

            for (int i = startIndex; i < endIndex; ++i)
            {
                // String Builder has overloads for int to prevent allocations
                bodyBuilder.Append('\"');

                //Asset-bundle-registry pointer content is case sensitive
                bodyBuilder.Append(wearablesToRequest[i].LowerCaseUrn());
                bodyBuilder.Append('\"');

                if (i != endIndex - 1)
                    bodyBuilder.Append(",");
            }

            bodyBuilder.Append("]}");

            using EntitiesAnalytics.RequestEnvelope analytics = entitiesAnalytics.Track(AnalyticsEvents.Endpoints.AVATAR_ATTACHMENT_RETRIEVED, endIndex - startIndex);

            using PoolExtensions.Scope<List<TDTO>> dtoPooledList = DTO_POOL.AutoScope();

            await webRequestController.PostAsync(new CommonArguments(url), GenericPostArguments.CreateJson(bodyBuilder.ToString()), ct, GetReportCategory())
                                      .OverwriteFromJsonAsync(dtoPooledList.Value, WRJsonParser.Newtonsoft, WRThreadFlags.SwitchToThreadPool);

            analytics.OnRequestFinished(dtoPooledList.Value.Count);

            foreach (TDTO entityDefinitionBase in dtoPooledList.Value)
            {
                if (entityDefinitionBase.pointers.Length > 0 && abVersions.versions.TryGetValue(entityDefinitionBase.pointers[0], out var wearableVersions))
                    entityDefinitionBase.assetBundleManifestVersion = AssetBundleManifestVersion.CreateManualManifest(wearableVersions.mac.version, wearableVersions.mac.buildDate, wearableVersions.windows.version,  wearableVersions.windows.buildDate);

                // Run the check just to inject content. If the registry had the entry, the manifest was already built
                await AssetBundleManifestFallbackHelper.CheckAssetBundleManifestFallbackAsync(World, entityDefinitionBase, partitionComponent, ct);
            }

            lock (results) { results.AddRange(dtoPooledList.Value); }
        }
    }
}
