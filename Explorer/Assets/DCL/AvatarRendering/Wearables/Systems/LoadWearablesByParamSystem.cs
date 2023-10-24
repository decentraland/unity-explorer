using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using Diagnostics.ReportsHandling;
using ECS;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Utility.Multithreading;

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class LoadWearablesByParamSystem : LoadSystemBase<IWearable[], GetWearableByParamIntention>
    {
        private readonly URLSubdirectory lambdaSubdirectory;

        private readonly IRealmData realmData;
        private readonly URLSubdirectory wearablesSubdirectory;
        private readonly WearableCatalog wearableCatalog;

        private readonly Func<bool> isRealmDataReady;
        internal IURLBuilder urlBuilder = new URLBuilder();

        public LoadWearablesByParamSystem(
            World world, IStreamableCache<IWearable[], GetWearableByParamIntention> cache,
            MemoryBudgetProvider memoryBudgetProvider,
            IRealmData realmData,
            URLSubdirectory lambdaSubdirectory, URLSubdirectory wearablesSubdirectory, WearableCatalog wearableCatalog,
            MutexSync mutexSync) : base(world, memoryBudgetProvider, cache, mutexSync)
        {
            this.realmData = realmData;
            this.lambdaSubdirectory = lambdaSubdirectory;
            this.wearableCatalog = wearableCatalog;
            this.wearablesSubdirectory = wearablesSubdirectory;

            isRealmDataReady = () => realmData.Configured;
        }

        protected override async UniTask<StreamableLoadingResult<IWearable[]>> FlowInternalAsync(GetWearableByParamIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            await UniTask.WaitUntil(isRealmDataReady, cancellationToken: ct);

            string response;

            using (var request = UnityWebRequest.Get(BuildURL(intention.UserID, intention.Params)))
            {
                await request.SendWebRequest().WithCancellation(ct);
                response = request.downloadHandler.text;
            }

            //Deserialize out of the main thread
            await UniTask.SwitchToThreadPool();

            WearableDTO.LambdaResponse lambdaResponse = JsonUtility.FromJson<WearableDTO.LambdaResponse>(response);

            for (var i = 0; i < lambdaResponse.elements.Count; i++)
            {
                WearableDTO wearableDto = lambdaResponse.elements[i].entity;
                intention.Results.Add(wearableCatalog.GetOrAddWearableByDTO(wearableDto));
            }

            return new StreamableLoadingResult<IWearable[]>(intention.Results.ToArray());
        }

        private string BuildURL(string userID, (string paramName, string paramValue)[] urlEncodedParams)
        {
            urlBuilder.Clear();

            urlBuilder.AppendDomainWithReplacedPath(realmData.Ipfs.LambdasBaseUrl, lambdaSubdirectory)
                      .AppendSubDirectory(URLSubdirectory.FromString(userID))
                      .AppendSubDirectory(wearablesSubdirectory);

            if (urlEncodedParams.Length > 0)
            {
                for (var i = 0; i < urlEncodedParams.Length; i++)
                    urlBuilder.AppendParameter(urlEncodedParams[i]);
            }

            return urlBuilder.GetResult();
        }
    }
}
