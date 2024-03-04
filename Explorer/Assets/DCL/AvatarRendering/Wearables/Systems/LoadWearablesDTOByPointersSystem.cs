using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
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
using Utility.Multithreading;

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class LoadWearablesDTOByPointersSystem : LoadSystemBase<WearablesDTOList, GetWearableDTOByPointersIntention>
    {
        // When the number of wearables to request is greater than MAX_WEARABLES_PER_REQUEST, we split the request into several smaller ones.
        // In this way we avoid to send a very long url string that would fail due to the web request size limitations.
        private const int MAX_WEARABLES_PER_REQUEST = 200;

        private static readonly ThreadSafeListPool<WearableDTO> DTO_POOL = new (MAX_WEARABLES_PER_REQUEST, 50);

        private readonly StringBuilder bodyBuilder = new ();

        private readonly IWebRequestController webRequestController;

        internal LoadWearablesDTOByPointersSystem(World world, IWebRequestController webRequestController, IStreamableCache<WearablesDTOList, GetWearableDTOByPointersIntention> cache, MutexSync mutexSync) : base(world, cache, mutexSync)
        {
            this.webRequestController = webRequestController;
        }

        protected override async UniTask<StreamableLoadingResult<WearablesDTOList>> FlowInternalAsync(GetWearableDTOByPointersIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            var finalTargetList = new List<WearableDTO>();

            int numberOfPartialRequests = (intention.Pointers.Count + MAX_WEARABLES_PER_REQUEST - 1) / MAX_WEARABLES_PER_REQUEST;

            var pointer = 0;

            for (var i = 0; i < numberOfPartialRequests; i++)
            {
                int numberOfWearablesToRequest = Mathf.Min(intention.Pointers.Count - pointer, MAX_WEARABLES_PER_REQUEST);

                await DoPartialRequestAsync(intention.CommonArguments.URL, intention.Pointers,
                    pointer, pointer + numberOfWearablesToRequest, finalTargetList, ct);

                pointer += numberOfWearablesToRequest;
            }

            return new StreamableLoadingResult<WearablesDTOList>(new WearablesDTOList(finalTargetList));
        }

        private async UniTask DoPartialRequestAsync(URLAddress url,
            IReadOnlyList<string> wearablesToRequest, int startIndex, int endIndex, List<WearableDTO> results, CancellationToken ct)
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

            using PoolExtensions.Scope<List<WearableDTO>> dtoPooledList = DTO_POOL.AutoScope();
            List<WearableDTO> dtoTempBuffer = dtoPooledList.Value;

            await (await webRequestController.PostAsync(new CommonArguments(url), GenericPostArguments.CreateJson(bodyBuilder.ToString()), ct))
               .OverwriteFromJsonAsync(dtoTempBuffer, WRJsonParser.Newtonsoft, WRThreadFlags.SwitchToThreadPool);

            // List is not concurrent
            lock (results) { results.AddRange(dtoTempBuffer); }
        }
    }
}
