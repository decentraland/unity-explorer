using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using Newtonsoft.Json;
using System.Linq;
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
        private readonly string LAMBDA_URL;

        public LoadWearablesByParamSystem(World world, IStreamableCache<WearableDTO[], GetWearableByParamIntention> cache, MutexSync mutexSync, string lambdaURL) : base(world, cache, mutexSync)
        {
            LAMBDA_URL = lambdaURL;
        }

        protected override async UniTask<StreamableLoadingResult<WearableDTO[]>> FlowInternal(GetWearableByParamIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            string response;

            using (var request = UnityWebRequest.Get(BuildURL(LAMBDA_URL, intention.UserID, intention.Params)))
            {
                await request.SendWebRequest().WithCancellation(ct);
                response = request.downloadHandler.text;
            }

            //Deserialize out of the main thread
            await UniTask.SwitchToThreadPool();

            //TODO: Keep this in mind, because not completely sure what we will need in the future
            LambdaResponse lambdaResponse = JsonConvert.DeserializeObject<LambdaResponse>(response);
            var wearableDtos = new WearableDTO[lambdaResponse.elements.Count()];

            for (var i = 0; i < lambdaResponse.elements.Count; i++)
                wearableDtos[i] = lambdaResponse.elements[i].entity;

            return new StreamableLoadingResult<WearableDTO[]>(wearableDtos);
        }

        private string BuildURL(string url, string userID, params (string paramName, string paramValue)[] urlEncodedParams)
        {
            urlBuilder.Clear();
            urlBuilder.Append(url);

            if (!urlBuilder.ToString().EndsWith('/'))
                urlBuilder.Append('/');

            urlBuilder.Append("/");
            urlBuilder.Append(userID);
            urlBuilder.Append("/wearables/");

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
