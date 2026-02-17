using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Loading.Systems.Abstract;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using ECS;
using ECS.StreamableLoading.Cache;

namespace DCL.AvatarRendering.Emotes.Load
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.EMOTE)]
    public partial class LoadTrimmedEmotesByParamSystem : LoadTrimmedElementsByIntentionSystem<TrimmedEmotesResponse, GetTrimmedEmotesByParamIntention, ITrimmedEmote, TrimmedEmoteDTO, IEmote, EmoteDTO>
    {
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
        }

        protected override async UniTask<IAttachmentLambdaResponse<ILambdaResponseElement<TrimmedEmoteDTO>>> ParseResponseAsync(GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> adapter) =>
            await adapter.CreateFromJson<TrimmedEmoteDTO.LambdaResponse>(WRJsonParser.Newtonsoft);

        protected override async UniTask<IBuilderLambdaResponse<IBuilderLambdaResponseElement<EmoteDTO>>> ParseBuilderResponseAsync(GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> adapter) =>
            await adapter.CreateFromJson<BuilderEmoteDTO.BuilderLambdaResponse>(WRJsonParser.Newtonsoft);

        protected override TrimmedEmotesResponse AssetFromPreparedIntention(in GetTrimmedEmotesByParamIntention intention) =>
            // Promise creation for builder collections is handled at ResolveBuilderEmotePromisesSystem
            new (intention.Results, intention.TotalAmount);
    }
}
