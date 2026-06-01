using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.Systems.Abstract;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using ECS;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Emotes.Load
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.EMOTE)]
    public partial class LoadTrimmedEmotesByParamSystem : LoadTrimmedElementsByIntentionSystem<TrimmedEmotesResponse, GetTrimmedEmotesByParamIntention, ITrimmedEmote, TrimmedEmoteDTO, IEmote, EmoteDTO>
    {
        private readonly IEmoteStorage emoteStorage;

        public LoadTrimmedEmotesByParamSystem(
            World world,
            IRealmData realmData,
            IWebRequestController webRequestController,
            IStreamableCache<TrimmedEmotesResponse, GetTrimmedEmotesByParamIntention> cache,
            IEmoteStorage emoteStorage,
            ITrimmedEmoteStorage trimmedEmoteStorage,
            URLSubdirectory emotesSubdirectory,
            IDecentralandUrlsSource urlsSource,
            IOwnedNftFilter ownedNftFilter,
            string? builderContentURL = null
        ) : base(world, cache, trimmedEmoteStorage, emoteStorage, realmData, emotesSubdirectory,
            webRequestController,"emote", urlsSource, ownedNftFilter, builderContentURL: builderContentURL)
        {
            this.emoteStorage = emoteStorage;
        }

        protected override async UniTask<IAttachmentLambdaResponse<ILambdaResponseElement<TrimmedEmoteDTO>>> ParseResponseAsync(GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> adapter) =>
            await adapter.CreateFromJson<TrimmedEmoteDTO.LambdaResponse>(WRJsonParser.Newtonsoft);

        protected override async UniTask<IBuilderLambdaResponse<IBuilderLambdaResponseElement<EmoteDTO>>> ParseBuilderResponseAsync(GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> adapter) =>
            await adapter.CreateFromJson<BuilderEmoteDTO.BuilderLambdaResponse>(WRJsonParser.Newtonsoft);

        protected override TrimmedEmotesResponse AssetFromPreparedIntention(in GetTrimmedEmotesByParamIntention intention) =>
            new (intention.Results, intention.TotalAmount);

        protected override void AfterBuilderItemsLoaded(IPartitionComponent partition, IReadOnlyList<IEmote> builderItems)
        {
            for (int i = 0; i < builderItems.Count; i++)
                BuilderEmoteAssetPromiseFactory.TryCreate(World!, builderItems[i], partition, emoteStorage, urlBuilder);
        }
    }
}
