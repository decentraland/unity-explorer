using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DCL.AvatarRendering.Loading.Systems.Abstract
{
    public abstract class LoadElementsByPointersSystem<TAsset, TIntention, TDTO> : LoadSystemBase<TAsset, TIntention> where TIntention: struct, IPointersLoadingIntention
    {
        // When the number of wearables to request is greater than MAX_WEARABLES_PER_REQUEST, we split the request into several smaller ones.
        // In this way we avoid to send a very long url string that would fail due to the web request size limitations.
        protected const int MAX_WEARABLES_PER_REQUEST = 200;

        protected static readonly ThreadSafeListPool<TDTO> DTO_POOL = new (MAX_WEARABLES_PER_REQUEST, 50);

        private readonly IWebRequestController webRequestController;
        private readonly StringBuilder bodyBuilder = new ();

        protected LoadElementsByPointersSystem(World world, IStreamableCache<TAsset, TIntention> cache, IWebRequestController webRequestController) : base(world, cache)
        {
            this.webRequestController = webRequestController;
        }

        protected sealed override async UniTask<StreamableLoadingResult<TAsset>> FlowInternalAsync(
            TIntention intention, IAcquiredBudget acquiredBudget,
            IPartitionComponent partition, CancellationToken ct)
        {
            var finalTargetList = RepoolableList<TDTO>.NewList();

            int numberOfPartialRequests = (intention.Pointers.Count + MAX_WEARABLES_PER_REQUEST - 1) / MAX_WEARABLES_PER_REQUEST;

            var pointer = 0;

            for (var i = 0; i < numberOfPartialRequests; i++)
            {
                int numberOfWearablesToRequest = Mathf.Min(intention.Pointers.Count - pointer, MAX_WEARABLES_PER_REQUEST);

                await DoPartialRequestAsync(intention.CommonArguments.URL, intention.Pointers,
                    pointer, pointer + numberOfWearablesToRequest, finalTargetList.List, ct);

                pointer += numberOfWearablesToRequest;
            }

            return new StreamableLoadingResult<TAsset>(CreateAssetFromListOfDTOs(finalTargetList));
        }

        protected abstract TAsset CreateAssetFromListOfDTOs(RepoolableList<TDTO> list);

        private async UniTask DoPartialRequestAsync(URLAddress url,
            IReadOnlyList<URN> wearablesToRequest, int startIndex, int endIndex, List<TDTO> results, CancellationToken ct)
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

            using PoolExtensions.Scope<List<TDTO>> dtoPooledList = DTO_POOL.AutoScope();

            await webRequestController.PostAsync(new CommonArguments(url), GenericPostArguments.CreateJson(bodyBuilder.ToString()), ct)
                                      .OverwriteFromJsonAsync(dtoPooledList.Value, WRJsonParser.Newtonsoft, WRThreadFlags.SwitchToThreadPool);

            lock (results) { results.AddRange(dtoPooledList.Value); }
        }
    }
}
