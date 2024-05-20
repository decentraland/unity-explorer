using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Components;
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
using Utility.Multithreading;

namespace DCL.AvatarRendering.Emotes
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.EMOTE)]
    public partial class LoadOwnedEmotesSystem : LoadSystemBase<EmotesResolution, GetOwnedEmotesFromRealmIntention>
    {
        private readonly IRealmData realmData;
        private readonly IEmoteCache emoteCache;
        private readonly IWebRequestController webRequestController;

        public LoadOwnedEmotesSystem(
            World world,
            IRealmData realmData,
            IWebRequestController webRequestController,
            IStreamableCache<EmotesResolution, GetOwnedEmotesFromRealmIntention> cache,
            IEmoteCache emoteCache)
            : base(world, cache)
        {
            this.realmData = realmData;
            this.emoteCache = emoteCache;
            this.webRequestController = webRequestController;
        }

        protected override async UniTask<StreamableLoadingResult<EmotesResolution>> FlowInternalAsync(GetOwnedEmotesFromRealmIntention intention,
            IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            LambdaOwnedEmoteElementList lambdaResponse =
                await webRequestController.GetAsync(new CommonArguments(intention.CommonArguments.URL, attemptsCount: intention.CommonArguments.Attempts),
                        ct, GetReportCategory())
                   .CreateFromJson<LambdaOwnedEmoteElementList>(WRJsonParser.Unity);

            // The following logic is not thread-safe!
            // TODO make it thread-safe: cache and CreateWearableThumbnailPromise

            if (lambdaResponse.elements.Count == 0)
                return new StreamableLoadingResult<EmotesResolution>(new EmotesResolution(Array.Empty<IEmote>(), lambdaResponse.totalAmount));

            var emotes = new IEmote[lambdaResponse.elements.Count];

            for (var i = 0; i < lambdaResponse.elements.Count; i++)
            {
                LambdaOwnedEmoteElementDTO element = lambdaResponse.elements[i];
                EmoteDTO emoteDto = element.entity;

                IEmote emote = emoteCache.GetOrAddEmoteByDTO(emoteDto);

                foreach (LambdaOwnedEmoteElementDTO.IndividualDataDTO individualData in element.individualData)
                {
                    // Probably a base emote, wrongly return individual data. Skip it
                    if (emoteDto.metadata.id == individualData.id) continue;

                    long.TryParse(individualData.transferredAt, out long transferredAt);
                    decimal.TryParse(individualData.price, out decimal price);

                    emoteCache.SetOwnedNft(emoteDto.metadata.id,
                        new NftBlockchainOperationEntry(individualData.id,
                            individualData.tokenId,
                            DateTimeOffset.FromUnixTimeSeconds(transferredAt).DateTime,
                            price));
                }

                WearableComponentsUtils.CreateWearableThumbnailPromise(realmData, emote, World, partition);

                emotes[i] = emote;
            }

            return new StreamableLoadingResult<EmotesResolution>(new EmotesResolution(emotes, lambdaResponse.totalAmount));
        }
    }
}
