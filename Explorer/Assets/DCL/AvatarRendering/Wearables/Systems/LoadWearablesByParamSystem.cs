using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Utility.Multithreading;

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LoadWearablesByParamSystem : LoadSystemBase<IWearable[], GetWearableyParamIntention>
    {
        private readonly URLSubdirectory lambdaSubdirectory;

        private readonly IRealmData realmData;
        private readonly URLBuilder urlBuilder = new ();
        private readonly URLSubdirectory wearablesSubdirectory;

        internal Dictionary<string, IWearable> wearableCatalog;

        public LoadWearablesByParamSystem(
            World world, IStreamableCache<IWearable[], GetWearableyParamIntention> cache, IRealmData realmData,
            URLSubdirectory lambdaSubdirectory, URLSubdirectory wearablesSubdirectory, Dictionary<string, IWearable> wearableCatalog,
            MutexSync mutexSync) : base(world, cache, mutexSync)
        {
            this.realmData = realmData;
            this.lambdaSubdirectory = lambdaSubdirectory;
            this.wearableCatalog = wearableCatalog;
            this.wearablesSubdirectory = wearablesSubdirectory;
        }

        protected override async UniTask<StreamableLoadingResult<IWearable[]>> FlowInternal(GetWearableyParamIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            string response;

            using (var request = UnityWebRequest.Get(BuildURL(intention.UserID, intention.Params)))
            {
                await request.SendWebRequest().WithCancellation(ct);
                response = request.downloadHandler.text;
            }

            //Deserialize out of the main thread
            await UniTask.SwitchToThreadPool();

            //TODO: Keep this in mind, because not completely sure what we will need in the future
            WearableDTO.LambdaResponse lambdaResponse = JsonUtility.FromJson<WearableDTO.LambdaResponse>(response);

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

        private string BuildURL(string userID, (string paramName, string paramValue)[] urlEncodedParams)
        {
            urlBuilder.Clear();

            urlBuilder.AppendDomain(realmData.Ipfs.ContentBaseUrl)
                      .AppendSubDirectory(lambdaSubdirectory)
                      .AppendSubDirectory(URLSubdirectory.FromString(userID))
                      .AppendSubDirectory(wearablesSubdirectory);

            if (urlEncodedParams.Length > 0)
            {
                for (var i = 0; i < urlEncodedParams.Length; i++)
                    urlBuilder.AppendParameter(urlEncodedParams[i]);
            }

            return urlBuilder.ToString();
        }
    }
}
