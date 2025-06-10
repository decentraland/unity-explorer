using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Components;
using DCL.WebRequests;
using ECS;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using System;
using System.Threading;
using Utility.Multithreading;

namespace DCL.AvatarRendering.Loading.Systems.Abstract
{
    public abstract class LoadElementsByIntentionSystem<TAsset, TIntention, TAvatarElement, TAvatarElementDTO> :
        LoadSystemBase<TAsset, TIntention>
        where TIntention: struct, IAttachmentsLoadingIntention<TAvatarElement>
        where TAvatarElementDTO: AvatarAttachmentDTO where TAvatarElement : IAvatarAttachment<TAvatarElementDTO>
    {
        private readonly IAvatarElementStorage<TAvatarElement, TAvatarElementDTO> avatarElementStorage;
        private readonly IWebRequestController webRequestController;
        private readonly string? builderContentURL;
        private readonly string? expectedBuilderItemType;

        protected readonly IRealmData realmData;

        protected LoadElementsByIntentionSystem(
            World world,
            IStreamableCache<TAsset, TIntention> cache,
            IAvatarElementStorage<TAvatarElement, TAvatarElementDTO> avatarElementStorage,
            IWebRequestController webRequestController,
            IRealmData realmData,
            string? builderContentURL = null,
            string? expectedBuilderItemType = null
        ) : base(world, cache)
        {
            this.avatarElementStorage = avatarElementStorage;
            this.webRequestController = webRequestController;
            this.realmData = realmData;
            this.builderContentURL = builderContentURL;
            this.expectedBuilderItemType = expectedBuilderItemType;
        }

        protected sealed override async UniTask<StreamableLoadingResult<TAsset>> FlowInternalAsync(TIntention intention,
            StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            await realmData.WaitConfiguredAsync();

            URLAddress url = BuildUrlFromIntention(in intention);

            if (intention.NeedsBuilderAPISigning)
            {
                var lambdaResponse =
                    await ParseBuilderResponseAsync(
                        webRequestController.SignedFetchGetAsync(
                            new CommonArguments(
                                url,
                                attemptsCount: intention.CommonArguments.Attempts
                            ),
                            string.Empty,
                            ct
                        )
                    );

                await using (await ExecuteOnThreadPoolScope.NewScopeWithReturnOnMainThreadAsync())
                    LoadBuilderItem(ref intention, lambdaResponse);
            }
            else
            {
                var lambdaResponse =
                    await ParseResponseAsync(
                        webRequestController.GetAsync(
                            new CommonArguments(
                                url,
                                attemptsCount: intention.CommonArguments.Attempts
                            ),
                            ct,
                            GetReportCategory()
                        )
                    );

                await using (await ExecuteOnThreadPoolScope.NewScopeWithReturnOnMainThreadAsync())
                    Load(ref intention, lambdaResponse);
            }

            return new StreamableLoadingResult<TAsset>(AssetFromPreparedIntention(in intention));
        }

        private void Load<TResponseElement>(ref TIntention intention, IAttachmentLambdaResponse<TResponseElement> lambdaResponse) where TResponseElement: ILambdaResponseElement<TAvatarElementDTO>
        {
            intention.SetTotal(lambdaResponse.TotalAmount);

            foreach (var element in lambdaResponse.Page)
            {
                var elementDTO = element.Entity;

                var wearable = avatarElementStorage.GetOrAddByDTO(elementDTO);

                foreach (var individualData in element.IndividualData)
                {
                    // Probably a base wearable, wrongly return individual data. Skip it
                    if (elementDTO.Metadata.id == individualData.id) continue;

                    long.TryParse(individualData.transferredAt, out long transferredAt);
                    decimal.TryParse(individualData.price, out decimal price);

                    avatarElementStorage.SetOwnedNft(
                        elementDTO.Metadata.id,
                        new NftBlockchainOperationEntry(
                            individualData.id,
                            individualData.tokenId,
                            DateTimeOffset.FromUnixTimeSeconds(transferredAt).DateTime,
                            price
                        )
                    );
                }

                intention.AppendToResult(wearable);
            }
        }

        private void LoadBuilderItem(ref TIntention intention, IBuilderLambdaResponse<IBuilderLambdaResponseElement<TAvatarElementDTO>> lambdaResponse)
        {
            if (string.IsNullOrEmpty(builderContentURL)) return;

            if (lambdaResponse.CollectionElements is { Count: > 0 })
            {
                int totalCount = 0;
                foreach (var element in lambdaResponse.CollectionElements)
                {
                    var elementDTO = element.BuildElementDTO(builderContentURL);

                    if (!string.IsNullOrEmpty(expectedBuilderItemType) && elementDTO.type != expectedBuilderItemType)
                        continue;

                    var avatarElement = avatarElementStorage.GetOrAddByDTO(elementDTO, false);
                    intention.AppendToResult(avatarElement);
                    totalCount++;
                }
                intention.SetTotal(totalCount);
            }
        }

        protected abstract UniTask<IAttachmentLambdaResponse<ILambdaResponseElement<TAvatarElementDTO>>> ParseResponseAsync(GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> adapter);

        protected abstract UniTask<IBuilderLambdaResponse<IBuilderLambdaResponseElement<TAvatarElementDTO>>> ParseBuilderResponseAsync(GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> adapter);

        protected abstract TAsset AssetFromPreparedIntention(in TIntention intention);

        protected abstract URLAddress BuildUrlFromIntention(in TIntention intention);
    }
}
