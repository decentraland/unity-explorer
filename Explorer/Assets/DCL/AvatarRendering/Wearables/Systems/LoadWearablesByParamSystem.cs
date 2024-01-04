using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.WebRequests;
using ECS;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using System;
using System.Threading;
using UnityEngine;
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
        private readonly IWearableCatalog wearableCatalog;
        private readonly IWebRequestController webRequestController;

        private readonly Func<bool> isRealmDataReady;
        internal IURLBuilder urlBuilder = new URLBuilder();

        public LoadWearablesByParamSystem(
            World world, IWebRequestController webRequestController, IStreamableCache<IWearable[], GetWearableByParamIntention> cache,
            IRealmData realmData, URLSubdirectory lambdaSubdirectory, URLSubdirectory wearablesSubdirectory,
            IWearableCatalog wearableCatalog, MutexSync mutexSync) : base(world, cache, mutexSync)
        {
            this.realmData = realmData;
            this.lambdaSubdirectory = lambdaSubdirectory;
            this.wearableCatalog = wearableCatalog;
            this.webRequestController = webRequestController;
            this.wearablesSubdirectory = wearablesSubdirectory;

            isRealmDataReady = () => realmData.Configured;
        }

        protected override async UniTask<StreamableLoadingResult<IWearable[]>> FlowInternalAsync(GetWearableByParamIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            await UniTask.WaitUntil(isRealmDataReady, cancellationToken: ct);

            WearableDTO.LambdaResponse lambdaResponse =
                await (await webRequestController.GetAsync(new CommonArguments(BuildURL(intention.UserID, intention.Params), attemptsCount: 1), ct, GetReportCategory()))
                   .CreateFromJson<WearableDTO.LambdaResponse>(WRJsonParser.Unity, WRThreadFlags.SwitchToThreadPool);

            for (var i = 0; i < lambdaResponse.elements.Count; i++)
            {
                WearableDTO wearableDto = lambdaResponse.elements[i].entity;
                IWearable wearable = wearableCatalog.GetOrAddWearableByDTO(wearableDto);
                var wearableThumbnailComponent = new WearableThumbnailComponent(wearable);

                World.Create(wearableThumbnailComponent, PartitionComponent.TOP_PRIORITY);
                intention.Results.Add(wearable);
            }
            return new StreamableLoadingResult<IWearable[]>(intention.Results.ToArray());
        }

        private URLAddress BuildURL(string userID, (string paramName, string paramValue)[] urlEncodedParams)
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

            return urlBuilder.Build();
        }
    }
}
