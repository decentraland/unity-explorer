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
using System.Text;
using System.Threading;
using UnityEngine.Networking;
using Utility.Multithreading;

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LoadWearablesByParamSystem : LoadSystemBase<WearableDTO[], GetWearableByParamIntention>
    {
        private readonly StringBuilder urlBuilder = new ();

        public LoadWearablesByParamSystem(World world, IStreamableCache<WearableDTO[], GetWearableByParamIntention> cache, MutexSync mutexSync) : base(world, cache, mutexSync) { }

        protected override async UniTask<StreamableLoadingResult<WearableDTO[]>> FlowInternal(GetWearableByParamIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            //TODO: Failure flow
            string response;

            using (var request = UnityWebRequest.Get(BuildURL(intention.CommonArguments.URL, intention.Params)))
            {
                await request.SendWebRequest().WithCancellation(ct);
                response = request.downloadHandler.text;
            }

            //Deserialize out of the main thread
            await UniTask.SwitchToThreadPool();
            return new StreamableLoadingResult<WearableDTO[]>(JsonConvert.DeserializeObject<BaseWearablesListResponse>(response).entities);
        }

        private string BuildURL(string url, params (string paramName, string paramValue)[] urlEncodedParams)
        {
            urlBuilder.Clear();
            urlBuilder.Append(url);

            if (!urlBuilder.ToString().EndsWith('/'))
                urlBuilder.Append('/');

            if (urlEncodedParams.Length > 0)
            {
                urlBuilder.Append(url.Contains('?') ? '&' : '?');

                for (var i = 0; i < urlEncodedParams.Length; i++)
                {
                    (string paramName, string paramValue) param = urlEncodedParams[i];
                    urlBuilder.Append(param.paramName);
                    urlBuilder.Append('=');
                    urlBuilder.Append(param.paramValue);

                    if (i < urlEncodedParams.Length - 1)
                        urlBuilder.Append('&');
                }
            }

            return urlBuilder.ToString();
        }
    }
}
