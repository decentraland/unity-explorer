using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Components;
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
using Utility.Times;

namespace DCL.AvatarRendering.Loading.Systems.Abstract
{
    public abstract class LoadElementsByIntentionSystem<TAsset, TIntention, TAvatarElement, TAvatarElementDTO> :
        LoadSystemBase<TAsset, TIntention>
        where TIntention: struct, IAttachmentsLoadingIntention<TAvatarElement>
        where TAvatarElementDTO: AvatarAttachmentDTO where TAvatarElement : IAvatarAttachment<TAvatarElementDTO>
    {
        private readonly IAvatarElementStorage<TAvatarElement, TAvatarElementDTO> avatarElementStorage;
        private readonly IWebRequestController webRequestController;
        private readonly IRealmData realmData;

        protected LoadElementsByIntentionSystem(
            World world,
            IStreamableCache<TAsset, TIntention> cache,
            IAvatarElementStorage<TAvatarElement, TAvatarElementDTO> avatarElementStorage,
            IWebRequestController webRequestController,
            IRealmData realmData
        ) : base(world, cache)
        {
            this.avatarElementStorage = avatarElementStorage;
            this.webRequestController = webRequestController;
            this.realmData = realmData;
        }

        protected sealed override async UniTask<StreamableLoadingResult<TAsset>> FlowInternalAsync(TIntention intention,
            IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            await realmData.WaitConfiguredAsync();

            string reportCategory = GetReportCategory();
            URLAddress url = BuildUrlFromIntention(in intention);
            WebRequestSignInfo? signInfo = null;
            WebRequestHeadersInfo? headersInfo = null;

            if (intention.CommonArguments.NeedsBuilderAPISigning)
            {
                /*signInfo = WebRequestSignInfo.NewFromRaw(
                    string.Empty,
                    intention.CommonArguments.URL,
                    // signUrl!,
                    // "https://builder-api.decentraland.org/v1",
                    // "https://builder-api.decentraland.org/v1/collections/3062136a-065d-4d94-b28c-f57d6ef04860/items/",
                    DateTime.UtcNow.UnixTimeAsMilliseconds(),
                    "get"
                )*/

                // signInfo = new WebRequestSignInfo(intention.CommonArguments.URL);
                // signInfo = new WebRequestSignInfo("get:/v1/collections/3062136a-065d-4d94-b28c-f57d6ef04860/items::");

                // sign URL patch copied from unity-renderer
                string headersGenerationUrl = url.Value;
                int index = headersGenerationUrl.IndexOf("?", StringComparison.Ordinal);
                if (index >= 0)
                    headersGenerationUrl = headersGenerationUrl.Substring(0, index);


                // THIS REQUEST ALSO NEEDS TO BE SIGNED...
                var signingHeaderGenerationResponse = await webRequestController.GetAsync(
                    new CommonArguments(
                        URLAddress.FromString(headersGenerationUrl),
                        attemptsCount: intention.CommonArguments.Attempts
                    ),
                    ct,
                    reportCategory
                ).CreateFromJson<BuilderAPISigningHeadersResponse>(WRJsonParser.Newtonsoft);

                headersInfo = new WebRequestHeadersInfo()
                    .Add("x-identity-auth-chain-0", signingHeaderGenerationResponse.headers[0].ToString()) // {"type":"SIGNER","payload":"0x51777c0b8dba8b4dfe8a1c3d0a1edaa5b139b4e0","signature":""}
                    .Add("x-identity-auth-chain-1", signingHeaderGenerationResponse.headers[1].ToString()) // {"type":"ECDSA_EPHEMERAL","payload":"Decentraland Login\nEphemeral address: 0x3A6040397234DADaaC6CB3F131c509d0b71DD200\nExpiration: 2025-02-09T14:36:33.371Z","signature":"0x81030b247bede0ddd5d97e1baa450156d7ed440a28a5318b118212e6468ade7317cd38544fad26010c3acf01da5aa56cfe8a87673dc7848e9855379b303cc0741b"}
                    .Add("x-identity-auth-chain-2", signingHeaderGenerationResponse.headers[2].ToString()) // {"type":"ECDSA_SIGNED_ENTITY","payload":"get:/v1/collections/3062136a-065d-4d94-b28c-f57d6ef04860/items:1736532810934:{}","signature":"0x7c916694de5a6ace92959202a8f9552dfb47dc4c33b7744080b866ba0e86c97a39bab44d7847d9f4a052534267c0f54dc4ac2fdc868f3ef940c6208361b79c511b"}
                    .WithSign(string.Empty, DateTime.UtcNow.UnixTimeAsMilliseconds());
            }

            var lambdaResponse =
                await ParsedResponseAsync(
                    webRequestController.GetAsync(
                        new CommonArguments(
                            url,
                            attemptsCount: intention.CommonArguments.Attempts
                        ),
                        ct,
                        reportCategory,
                        signInfo: signInfo,
                        headersInfo: headersInfo
                    )
                );

            await using (await ExecuteOnThreadPoolScope.NewScopeWithReturnOnMainThreadAsync())
                Load(ref intention, lambdaResponse);

            return new StreamableLoadingResult<TAsset>(AssetFromPreparedIntention(in intention));
        }
        [Serializable]
        public class BuilderAPISigningHeadersResponse
        {
            /*[Serializable]
            public class Header
            {
                public string type;
                public string payload;
                public string signature;
            }*/

            // public List<Header> headers;
            public List<string> headers;
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

        protected abstract UniTask<IAttachmentLambdaResponse<ILambdaResponseElement<TAvatarElementDTO>>> ParsedResponseAsync(GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> adapter);

        protected abstract TAsset AssetFromPreparedIntention(in TIntention intention);

        protected abstract URLAddress BuildUrlFromIntention(in TIntention intention);
    }
}
