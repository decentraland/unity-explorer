using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Loading.Systems.Abstract;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using DCL.WebRequests;
using ECS;
using ECS.StreamableLoading.Cache;

namespace DCL.AvatarRendering.Emotes.Load
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.EMOTE)]
    public partial class LoadOwnedEmotesSystem : LoadElementsByIntentionSystem<EmotesResolution, GetOwnedEmotesFromRealmIntention, IEmote, EmoteDTO>
    {
        public LoadOwnedEmotesSystem(
            World world,
            IRealmData realmData,
            IWebRequestController webRequestController,
            IStreamableCache<EmotesResolution, GetOwnedEmotesFromRealmIntention> cache,
            IEmoteStorage emoteStorage
        ) : base(world, cache, emoteStorage, webRequestController, realmData) { }

        protected override async UniTask<IAttachmentLambdaResponse<ILambdaResponseElement<EmoteDTO>>> ParseResponseAsync(GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> adapter) =>
            await adapter.CreateFromJson<LambdaOwnedEmoteElementList>(WRJsonParser.Unity);

        protected override async UniTask<IBuilderLambdaResponse<IBuilderLambdaResponseElement>> ParseBuilderResponseAsync(GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> adapter)
        {
            var result = await adapter.CreateFromJson<WearableDTO.BuilderLambdaResponse>(WRJsonParser.Newtonsoft);
            return result;
        }

        protected override EmotesResolution AssetFromPreparedIntention(in GetOwnedEmotesFromRealmIntention intention) =>
            new (intention.Result, intention.TotalAmount);

        protected override URLAddress BuildUrlFromIntention(in GetOwnedEmotesFromRealmIntention intention) =>
            intention.CommonArguments.URL;
    }
}
