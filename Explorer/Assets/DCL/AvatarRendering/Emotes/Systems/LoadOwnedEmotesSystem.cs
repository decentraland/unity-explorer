using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes.OwnedNfts;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.AvatarRendering.Emotes
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.EMOTE)]
    public partial class LoadOwnedEmotesSystem : LoadSystemBase<EmotesResolution, GetOwnedEmotesFromRealmIntention>
    {
        private readonly IEmoteCache emoteCache;
        private readonly IOwnedNftHub ownedNftHub;
        private readonly IWebRequestController webRequestController;

        public LoadOwnedEmotesSystem(
            World world,
            IWebRequestController webRequestController,
            IStreamableCache<EmotesResolution, GetOwnedEmotesFromRealmIntention> cache,
            IEmoteCache emoteCache,
            IOwnedNftHub ownedNftHub
        )
            : base(world, cache)
        {
            this.emoteCache = emoteCache;
            this.ownedNftHub = ownedNftHub;
            this.webRequestController = webRequestController;
        }

        protected override async UniTask<StreamableLoadingResult<EmotesResolution>> FlowInternalAsync(GetOwnedEmotesFromRealmIntention intention,
            IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            LambdaOwnedEmoteElementList lambdaResponse = await webRequestController
                                                              .GetAsync(
                                                                   new CommonArguments(
                                                                       intention.CommonArguments.URL,
                                                                       attemptsCount: intention.CommonArguments.Attempts
                                                                   ),
                                                                   ct,
                                                                   GetReportCategory()
                                                               )
                                                              .CreateFromJson<LambdaOwnedEmoteElementList>(WRJsonParser.Unity);


            if (lambdaResponse.elements.Count == 0) return EmptyResult(lambdaResponse);
            var emotes = ParsedEmotes(lambdaResponse);
            return new StreamableLoadingResult<EmotesResolution>(new EmotesResolution(emotes, lambdaResponse.totalAmount));
        }

        private IReadOnlyList<IEmote> ParsedEmotes(LambdaOwnedEmoteElementList lambdaResponse)
        {
            // The following logic is not thread-safe!
            // TODO make it thread-safe: cache and CreateWearableThumbnailPromise
            var emotes = new IEmote[lambdaResponse.elements.Count];

            for (var i = 0; i < lambdaResponse.elements.Count; i++)
            {
                LambdaOwnedEmoteElementDTO element = lambdaResponse.elements[i];
                EmoteDTO emoteDto = element.entity;

                IEmote emote = emoteCache.GetOrAddEmoteByDTO(emoteDto);

                foreach (LambdaOwnedEmoteElementDTO.IndividualDataDTO individualData in element.individualData)
                {
                    // Probably a base emote, wrongly return individual data. Skip it
                    if (emoteDto.metadata?.id == individualData.id) continue;
                    SetOwnedNft(emoteDto.metadata!.id!, individualData);
                }

                emotes[i] = emote;
            }

            return emotes;
        }

        private static StreamableLoadingResult<EmotesResolution> EmptyResult(LambdaOwnedEmoteElementList lambdaResponse) =>
            new (
                new EmotesResolution(
                    Array.Empty<IEmote>(),
                    lambdaResponse.totalAmount
                )
            );

        private void SetOwnedNft(string emoteMetadataId, LambdaOwnedEmoteElementDTO.IndividualDataDTO individualData)
        {
            long.TryParse(individualData.transferredAt, out long transferredAt);
            decimal.TryParse(individualData.price, out decimal price);

            ownedNftHub.SetOwnedNft(
                emoteMetadataId,
                new NftBlockchainOperationEntry(
                    individualData.id,
                    individualData.tokenId,
                    DateTimeOffset.FromUnixTimeSeconds(transferredAt).DateTime,
                    price
                )
            );
        }
    }
}
