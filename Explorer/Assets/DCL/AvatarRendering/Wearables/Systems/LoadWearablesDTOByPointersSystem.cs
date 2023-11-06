using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.PerformanceBudgeting.AcquiredBudget;
using Diagnostics.ReportsHandling;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Utility.Multithreading;
using Utility.Pool;
using Utility.ThreadSafePool;

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

        internal LoadWearablesDTOByPointersSystem(World world, IStreamableCache<WearablesDTOList, GetWearableDTOByPointersIntention> cache, MutexSync mutexSync) : base(world, cache, mutexSync) { }

        protected override async UniTask<StreamableLoadingResult<WearablesDTOList>> FlowInternalAsync(GetWearableDTOByPointersIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            var finalTargetList = new List<WearableDTO>();

            int numberOfPartialRequests = (intention.Pointers.Count + MAX_WEARABLES_PER_REQUEST - 1) / MAX_WEARABLES_PER_REQUEST;

            var pointer = 0;

            for (var i = 0; i < numberOfPartialRequests; i++)
            {
                int numberOfWearablesToRequest = Mathf.Min(intention.Pointers.Count - pointer, MAX_WEARABLES_PER_REQUEST);

                await DoPartialRequestAsync(intention.CommonArguments.URL, intention.Pointers,
                    pointer, pointer + numberOfWearablesToRequest, finalTargetList, partition, ct);

                pointer += numberOfWearablesToRequest;
            }

            return new StreamableLoadingResult<WearablesDTOList>(new WearablesDTOList(finalTargetList));
        }

        private async UniTask DoPartialRequestAsync(string url,
            IReadOnlyList<string> wearablesToRequest, int startIndex, int endIndex, List<WearableDTO> results,
            IPartitionComponent partition, CancellationToken ct)
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

            var subIntent = new SubIntention(new CommonLoadingArguments(url));

            async UniTask<StreamableLoadingResult<string>> InnerFlowAsync(SubIntention subIntention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
            {
                using UnityWebRequest request = await UnityWebRequest.Post(subIntent.CommonArguments.URL, bodyBuilder.ToString(), "application/json").SendWebRequest().WithCancellation(ct);
                return new StreamableLoadingResult<string>(request.downloadHandler.text);
            }

            string response = (await subIntent.RepeatLoopAsync(NoAcquiredBudget.INSTANCE, partition, InnerFlowAsync, GetReportCategory(), ct)).UnwrapAndRethrow();

            await UniTask.SwitchToThreadPool();

            // Parse and add into results

            using PoolExtensions.Scope<List<WearableDTO>> dtoPooledList = DTO_POOL.AutoScope();

            JsonConvert.PopulateObject(response, dtoPooledList.Value);

            // List is not concurrent
            lock (results) { results.AddRange(dtoPooledList.Value); }
        }
    }
}
