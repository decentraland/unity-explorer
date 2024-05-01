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
using System.Collections.Generic;
using System.Threading;
using Utility.Multithreading;

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class LoadWearablesByParamSystem : LoadSystemBase<WearablesResponse, GetWearableByParamIntention>
    {
        private readonly URLSubdirectory lambdaSubdirectory;
        private readonly IRealmData realmData;
        private readonly URLSubdirectory wearablesSubdirectory;
        private readonly IWearableCatalog wearableCatalog;
        private readonly IWebRequestController webRequestController;
        private readonly Func<bool> isRealmDataReady;

        internal IURLBuilder urlBuilder = new URLBuilder();

        public LoadWearablesByParamSystem(
            World world, IWebRequestController webRequestController, IStreamableCache<WearablesResponse, GetWearableByParamIntention> cache,
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

        protected override async UniTask<StreamableLoadingResult<WearablesResponse>> FlowInternalAsync(GetWearableByParamIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            await UniTask.WaitUntil(isRealmDataReady, cancellationToken: ct);

            WearableDTO.LambdaResponse lambdaResponse =
                await webRequestController.GetAsync(new CommonArguments(BuildURL(intention.UserID, intention.Params), attemptsCount: 1), ct, GetReportCategory())
                   .CreateFromJson<WearableDTO.LambdaResponse>(WRJsonParser.Unity, WRThreadFlags.SwitchToThreadPool);

            intention.TotalAmount = lambdaResponse.totalAmount;

            for (var i = 0; i < lambdaResponse.elements.Count; i++)
            {
                WearableDTO.LambdaResponseElementDto element = lambdaResponse.elements[i];
                WearableDTO wearableDto = element.entity;

                IWearable wearable = wearableCatalog.GetOrAddWearableByDTO(wearableDto);

                foreach (WearableDTO.LambdaResponseIndividualDataDto individualData in element.individualData)
                {
                    // Probably a base wearable, wrongly return individual data. Skip it
                    if (wearableDto.metadata.id == individualData.id) continue;

                    long.TryParse(individualData.transferredAt, out long transferredAt);
                    decimal.TryParse(individualData.price, out decimal price);

                    wearableCatalog.SetOwnedNft(wearableDto.metadata.id,
                        new NftBlockchainOperationEntry(individualData.id,
                            individualData.tokenId, DateTimeOffset.FromUnixTimeSeconds(transferredAt).DateTime,
                            price));
                }

                WearableComponentsUtils.CreateWearableThumbnailPromise(realmData, wearable, World, partition);
                intention.Results.Add(wearable);
            }

            return new StreamableLoadingResult<WearablesResponse>(new WearablesResponse(intention.Results.ToArray(), intention.TotalAmount));
        }

        private URLAddress BuildURL(string userID, IReadOnlyList<(string, string)> urlEncodedParams)
        {
            urlBuilder.Clear();

            urlBuilder.AppendDomainWithReplacedPath(realmData.Ipfs.LambdasBaseUrl, lambdaSubdirectory)
                      .AppendSubDirectory(URLSubdirectory.FromString(userID))
                      .AppendSubDirectory(wearablesSubdirectory);

            if (urlEncodedParams.Count > 0)
            {
                for (var i = 0; i < urlEncodedParams.Count; i++)
                    urlBuilder.AppendParameter(urlEncodedParams[i]);
            }

            return urlBuilder.Build();
        }
    }
}
