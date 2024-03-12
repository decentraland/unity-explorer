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
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility.Multithreading;
using StreamableResult = ECS.StreamableLoading.Common.Components.StreamableLoadingResult<DCL.AvatarRendering.Emotes.EmotesResolution>;
using EmotesFromRealmPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution,
    DCL.AvatarRendering.Emotes.GetEmotesByPointersFromRealmIntention>;

namespace DCL.AvatarRendering.Emotes
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.EMOTE)]
    public partial class LoadEmotesByPointersSystem : LoadSystemBase<EmotesDTOList, GetEmotesByPointersFromRealmIntention>
    {
        // When the number of wearables to request is greater than MAX_WEARABLES_PER_REQUEST, we split the request into several smaller ones.
        // In this way we avoid to send a very long url string that would fail due to the web request size limitations.
        private const int MAX_WEARABLES_PER_REQUEST = 200;

        private static readonly ThreadSafeListPool<EmoteDTO> DTO_POOL = new (MAX_WEARABLES_PER_REQUEST, 50);

        private readonly StringBuilder bodyBuilder = new ();

        private readonly IWebRequestController webRequestController;
        private readonly IEmoteCache emoteCache;
        private readonly IRealmData realmData;

        public LoadEmotesByPointersSystem(World world,
            IWebRequestController webRequestController,
            IStreamableCache<EmotesDTOList, GetEmotesByPointersFromRealmIntention> cache,
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

            GetEmotesFromRealmQuery(World);
            FinalizeWearableDTOQuery(World);
        }

        protected override async UniTask<StreamableLoadingResult<EmotesDTOList>> FlowInternalAsync(
            GetEmotesByPointersFromRealmIntention intention, IAcquiredBudget acquiredBudget,
            IPartitionComponent partition, CancellationToken ct)
        {
            var finalTargetList = new List<EmoteDTO>();

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
            IReadOnlyList<URN> wearablesToRequest, int startIndex, int endIndex, List<EmoteDTO> results, CancellationToken ct)
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

            using PoolExtensions.Scope<List<EmoteDTO>> dtoPooledList = DTO_POOL.AutoScope();

            await (await webRequestController.PostAsync(new CommonArguments(url), GenericPostArguments.CreateJson(bodyBuilder.ToString()), ct))
               .OverwriteFromJsonAsync(dtoPooledList.Value, WRJsonParser.Newtonsoft, WRThreadFlags.SwitchToThreadPool);

            lock (results) { results.AddRange(dtoPooledList.Value); }
        }

        [Query]
        private void GetEmotesFromRealm(in Entity entity,
            ref GetEmotesByPointersIntention intention,
            ref IPartitionComponent partitionComponent)
        {
            if (intention.CancellationTokenSource.IsCancellationRequested)
            {
                if (!World.Has<StreamableResult>(entity))
                    World.Add(entity, new StreamableResult(new OperationCanceledException("Pointer request cancelled")));

                return;
            }

            List<URN> missingPointers = ListPool<URN>.Get();
            List<IEmote> resolvedEmotes = ListPool<IEmote>.Get();

            var successfulResults = 0;
            var successfulDtos = 0;

            foreach (URN loadingIntentionPointer in intention.Pointers)
            {
                if (intention.ProcessedPointers.Contains(loadingIntentionPointer)) continue;
                intention.ProcessedPointers.Add(loadingIntentionPointer);

                if (loadingIntentionPointer.IsNullOrEmpty())
                {
                    ReportHub.LogError(
                        GetReportCategory(),
                        "ResolveWearableByPointerSystem: Null pointer found in the list of pointers"
                    );

                    continue;
                }

                URN shortenedPointer = loadingIntentionPointer;
                shortenedPointer = shortenedPointer.Shorten();

                if (!emoteCache.TryGetEmote(shortenedPointer, out IEmote emote))
                {
                    // wearableCatalog.AddEmptyWearable(shortenedPointer);
                    missingPointers.Add(shortenedPointer);
                    continue;
                }

                if (emote.Model.Succeeded)
                {
                    successfulDtos++;
                    resolvedEmotes.Add(emote);
                }
            }

            if (missingPointers.Count > 0)
            {
                var promise = EmotesFromRealmPromise.Create(World, new GetEmotesByPointersFromRealmIntention(missingPointers,
                        new CommonLoadingArguments(realmData.Ipfs.EntitiesActiveEndpoint)),
                    partitionComponent);

                World.Create(promise, partitionComponent);

                ListPool<URN>.Release(missingPointers);
                ListPool<IEmote>.Release(resolvedEmotes);

                return;
            }

            if (successfulDtos == intention.Pointers.Count) { }

            if (successfulResults == intention.Pointers.Count)
            {
                // World.Add(entity, new StreamableResult(new EmotesResolution(hideWearablesResolution.VisibleWearables, hideWearablesResolution.HiddenCategories)));
            }

            ListPool<URN>.Release(missingPointers);
            ListPool<IEmote>.Release(resolvedEmotes);
        }

        [Query]
        private void FinalizeWearableDTO(in Entity entity,
            ref AssetPromise<EmotesDTOList, GetEmotesByPointersFromRealmIntention> promise,
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
                    foreach (EmoteDTO assetEntity in promiseResult.Asset.Value)
                    {
                        IEmote component = emoteCache.GetOrAddEmoteByDTO(assetEntity);
                        component.Model = new StreamableLoadingResult<EmoteDTO>(assetEntity);
                        component.IsLoading = false;

                        WearableComponentsUtils.CreateWearableThumbnailPromise(realmData, component, World, partitionComponent);
                    }
                }

                World.Destroy(entity);
            }
        }
    }
}
