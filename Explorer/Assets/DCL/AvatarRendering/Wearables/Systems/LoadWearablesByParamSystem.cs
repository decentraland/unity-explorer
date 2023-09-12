using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine.Networking;
using Utility.Multithreading;

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LoadWearablesByParamSystem : LoadSystemBase<IWearable[], GetWearableyParamIntention>
    {

        private readonly StringBuilder urlBuilder = new ();
        private readonly string LAMBDA_URL;
        private readonly string WEARABLES_COMPLEMENT_URL;

        internal Dictionary<string, IWearable> wearableCatalog;

        public LoadWearablesByParamSystem(World world, IStreamableCache<IWearable[], GetWearableyParamIntention> cache, MutexSync mutexSync,
            string lambdaURL, string wearablesComplementURL, Dictionary<string, IWearable> wearableCatalog) : base(world, cache, mutexSync)
        {
            LAMBDA_URL = lambdaURL;
            this.wearableCatalog = wearableCatalog;
            WEARABLES_COMPLEMENT_URL = wearablesComplementURL;
        }

        protected override async UniTask<StreamableLoadingResult<IWearable[]>> FlowInternal(GetWearableyParamIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
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
            WearableDTO.LambdaResponse lambdaResponse = JsonConvert.DeserializeObject<WearableDTO.LambdaResponse>(response);
            for (var i = 0; i < lambdaResponse.elements.Count; i++)
            {
                WearableDTO wearableDto = lambdaResponse.elements[i].entity;

                if (wearableCatalog.TryGetValue(wearableDto.metadata.id, out IWearable result))
                    intention.Results.Add(result);
                else
                {
                    var wearable = new Wearable();
                    wearable.WearableDTO = new StreamableLoadingResult<WearableDTO>(wearableDto);
                    wearable.IsLoading = false;
                    wearableCatalog.Add(wearable.GetUrn(), wearable);
                    intention.Results.Add(wearable);
                }
            }

            return new StreamableLoadingResult<IWearable[]>(intention.Results.ToArray());
        }

        private string BuildURL(string url, string userID, params (string paramName, string paramValue)[] urlEncodedParams)
        {
            urlBuilder.Clear();
            urlBuilder.Append(url);
            urlBuilder.Append(userID);
            urlBuilder.Append(WEARABLES_COMPLEMENT_URL);

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
