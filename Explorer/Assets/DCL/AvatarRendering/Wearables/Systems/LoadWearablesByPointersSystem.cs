using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine.Networking;
using Utility.Multithreading;

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LoadWearablesByPointersSystem : LoadSystemBase<WearableDTO[], GetWearableByPointersIntention>
    {
        // When the number of wearables to request is greater than MAX_WEARABLES_PER_REQUEST, we split the request into several smaller ones.
        // In this way we avoid to send a very long url string that would fail due to the web request size limitations.
        private readonly int MAX_WEARABLES_PER_REQUEST;
        private readonly StringBuilder bodyBuilder = new ();

        public LoadWearablesByPointersSystem(World world, IStreamableCache<WearableDTO[], GetWearableByPointersIntention> cache, MutexSync mutexSync) : base(world, cache, mutexSync)
        {
            MAX_WEARABLES_PER_REQUEST = 200;
        }

        protected override async UniTask<StreamableLoadingResult<WearableDTO[]>> FlowInternal(GetWearableByPointersIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            var finalTargetList = new List<WearableDTO>();

            int numberOfPartialRequests = (intention.Pointers.Length + MAX_WEARABLES_PER_REQUEST - 1) / MAX_WEARABLES_PER_REQUEST;

            for (var i = 0; i < numberOfPartialRequests; i++)
            {
                await UniTask.SwitchToMainThread();

                int numberOfWearablesToRequest = intention.Pointers.Length < MAX_WEARABLES_PER_REQUEST
                    ? intention.Pointers.Length
                    : MAX_WEARABLES_PER_REQUEST;

                //TODO: Avoid Linq here?
                var wearablesToRequest = intention.Pointers.Take(numberOfWearablesToRequest).ToList();
                List<WearableDTO> partialResult = await DoPartialRequest(intention.CommonArguments.URL, wearablesToRequest, ct);
                finalTargetList.AddRange(partialResult);
            }

            return new StreamableLoadingResult<WearableDTO[]>(finalTargetList.ToArray());
        }

        private async UniTask<List<WearableDTO>> DoPartialRequest(string url, List<string> wearablesToRequest, CancellationToken ct)
        {
            bodyBuilder.Clear();
            bodyBuilder.Append("{\"pointers\":[");

            for (var i = 0; i < wearablesToRequest.Count; ++i)
            {
                // String Builder has overloads for int to prevent allocations
                bodyBuilder.Append('\"');
                bodyBuilder.Append(wearablesToRequest[i]);
                bodyBuilder.Append('\"');

                if (i != wearablesToRequest.Count - 1)
                    bodyBuilder.Append(",");
            }

            bodyBuilder.Append("]}");

            string response;

            using (var request = UnityWebRequest.Post(url, bodyBuilder.ToString(), "application/json"))
            {
                await request.SendWebRequest().WithCancellation(ct);
                response = request.downloadHandler.text;
            }

            await UniTask.SwitchToThreadPool();
            var partialTargetList = new List<WearableDTO>();
            JsonConvert.PopulateObject(response, partialTargetList);
            return partialTargetList;
        }

    }
}
