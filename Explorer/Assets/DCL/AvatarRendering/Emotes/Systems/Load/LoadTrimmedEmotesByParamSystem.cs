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

namespace DCL.AvatarRendering.Emotes.Load
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.EMOTE)]
    public partial class LoadTrimmedEmotesByParamSystem : LoadTrimmedElementsByIntentionSystem<TrimmedEmotesResponse, GetTrimmedEmotesByParamIntention, ITrimmedEmote, TrimmedEmoteDTO, IEmote, EmoteDTO>
    {
        private readonly IEmoteStorage emoteStorage;
        private readonly IURLBuilder builderPromiseUrlBuilder = new URLBuilder();

        public LoadTrimmedEmotesByParamSystem(
            World world,
            IRealmData realmData,
            IWebRequestController webRequestController,
            IStreamableCache<TrimmedEmotesResponse, GetTrimmedEmotesByParamIntention> cache,
            IEmoteStorage emoteStorage,
            ITrimmedEmoteStorage trimmedEmoteStorage,
            URLSubdirectory emotesSubdirectory,
            IDecentralandUrlsSource urlsSource,
            string? builderContentURL = null
        ) : base(world, cache, trimmedEmoteStorage, emoteStorage, realmData, emotesSubdirectory,
            webRequestController,"emote", urlsSource, builderContentURL: builderContentURL)
        {
            this.emoteStorage = emoteStorage;
        }

        protected override async UniTask<IAttachmentLambdaResponse<ILambdaResponseElement<TrimmedEmoteDTO>>> ParseResponseAsync(GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> adapter) =>
            await adapter.CreateFromJson<TrimmedEmoteDTO.LambdaResponse>(WRJsonParser.Newtonsoft);

        protected override async UniTask<IBuilderLambdaResponse<IBuilderLambdaResponseElement<EmoteDTO>>> ParseBuilderResponseAsync(GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> adapter) =>
            await adapter.CreateFromJson<BuilderEmoteDTO.BuilderLambdaResponse>(WRJsonParser.Newtonsoft);

        protected override TrimmedEmotesResponse AssetFromPreparedIntention(in GetTrimmedEmotesByParamIntention intention) =>
            new (intention.Results, intention.TotalAmount);

        protected override void AfterBuilderItemsLoaded(ref GetTrimmedEmotesByParamIntention intention, IPartitionComponent partition)
        {
            if (intention.Results is not { Count: > 0 }) return;

            foreach (ITrimmedEmote trimmedEmote in intention.Results)
            {
                if (trimmedEmote is not IEmote emote) continue;

                BuilderEmoteAssetPromiseFactory.TryCreate(World!, emote, partition, emoteStorage, builderPromiseUrlBuilder);
            }
        }
    }
}
