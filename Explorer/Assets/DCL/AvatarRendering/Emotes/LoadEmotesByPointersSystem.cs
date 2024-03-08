using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using DCL.WebRequests;
using ECS;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;
using Utility.Multithreading;

namespace DCL.AvatarRendering.Emotes
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.EMOTE)]
    public partial class LoadEmotesByPointersSystem : LoadSystemBase<EmotesDTOList, GetEmotesByPointersIntention>
    {
        // When the number of wearables to request is greater than MAX_WEARABLES_PER_REQUEST, we split the request into several smaller ones.
        // In this way we avoid to send a very long url string that would fail due to the web request size limitations.
        private const int MAX_WEARABLES_PER_REQUEST = 200;

        private static readonly ThreadSafeListPool<EmoteJsonDTO> DTO_POOL = new (MAX_WEARABLES_PER_REQUEST, 50);

        private readonly StringBuilder bodyBuilder = new ();

        private readonly IWebRequestController webRequestController;
        private readonly IEmoteCache emoteCache;
        private readonly IRealmData realmData;

        public LoadEmotesByPointersSystem(World world,
            IWebRequestController webRequestController,
            IStreamableCache<EmotesDTOList, GetEmotesByPointersIntention> cache,
            MutexSync mutexSync,
            IEmoteCache emoteCache,
            IRealmData realmData)
            : base(world, cache, mutexSync)
        {
            this.webRequestController = webRequestController;
            this.emoteCache = emoteCache;
            this.realmData = realmData;
        }

        protected override void Update(float t)
        {
            base.Update(t);

            FinalizeWearableDTOQuery(World);
        }

        protected override async UniTask<StreamableLoadingResult<EmotesDTOList>> FlowInternalAsync(GetEmotesByPointersIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            var finalTargetList = new List<EmoteJsonDTO>();

            int numberOfPartialRequests = (intention.Pointers.Count + MAX_WEARABLES_PER_REQUEST - 1) / MAX_WEARABLES_PER_REQUEST;

            var pointer = 0;

            for (var i = 0; i < numberOfPartialRequests; i++)
            {
                int numberOfWearablesToRequest = Mathf.Min(intention.Pointers.Count - pointer, MAX_WEARABLES_PER_REQUEST);

                await DoPartialRequestAsync(intention.CommonArguments.URL, intention.Pointers,
                    pointer, pointer + numberOfWearablesToRequest, finalTargetList, ct);

                pointer += numberOfWearablesToRequest;
            }

            return new StreamableLoadingResult<EmotesDTOList>(new EmotesDTOList(finalTargetList));
        }

        private async UniTask DoPartialRequestAsync(URLAddress url,
            IReadOnlyList<string> wearablesToRequest, int startIndex, int endIndex, List<EmoteJsonDTO> results, CancellationToken ct)
        {
            await UniTask.SwitchToMainThread();

            bodyBuilder.Clear();
            bodyBuilder.Append("{\"pointers\":[");

            for (int i = startIndex; i < endIndex; ++i)
            {
                // String Builder has overloads for int to prevent allocations
                bodyBuilder.Append('\"');
                bodyBuilder.Append(wearablesToRequest[i]);
                bodyBuilder.Append('\"');

                if (i != wearablesToRequest.Count - 1)
                    bodyBuilder.Append(",");
            }

            bodyBuilder.Append("]}");

            using PoolExtensions.Scope<List<EmoteJsonDTO>> dtoPooledList = DTO_POOL.AutoScope();

            await (await webRequestController.PostAsync(new CommonArguments(url), GenericPostArguments.CreateJson(bodyBuilder.ToString()), ct))
               .OverwriteFromJsonAsync(dtoPooledList.Value, WRJsonParser.Newtonsoft, WRThreadFlags.SwitchToThreadPool);

            lock (results) { results.AddRange(dtoPooledList.Value); }
        }

        [Query]
        private void FinalizeWearableDTO(in Entity entity,
            ref AssetPromise<EmotesDTOList, GetEmotesByPointersIntention> promise,
            ref IPartitionComponent partitionComponent)
        {
            if (promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested)
            {
                promise.ForgetLoading(World);
                World.Destroy(entity);
                return;
            }

            if (promise.TryConsume(World, out StreamableLoadingResult<EmotesDTOList> promiseResult))
            {
                if (!promiseResult.Succeeded)
                {
                    foreach (string pointerID in promise.LoadingIntention.Pointers)
                        if (emoteCache.TryGetEmote(pointerID, out IEmote component))
                            component.IsLoading = false;
                }
                else
                {
                    foreach (EmoteJsonDTO assetEntity in promiseResult.Asset.Value)
                    {
                        emoteCache.TryGetEmote(assetEntity.metadata.id, out IEmote component);

                        component.WearableDTO = new StreamableLoadingResult<EmoteJsonDTO>(assetEntity);
                        component.IsLoading = false;

                        WearableComponentsUtils.CreateWearableThumbnailPromise(realmData, component, World, partitionComponent);
                    }
                }

                World.Destroy(entity);
            }
        }
    }
}
