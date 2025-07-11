using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Loading.Systems.Abstract;
using DCL.Diagnostics;
using DCL.WebRequests;
using ECS;
using ECS.StreamableLoading.Cache;
using System;
using System.Threading;

namespace DCL.AvatarRendering.Emotes.Load
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.EMOTE)]
    public partial class LoadOwnedEmotesSystem : LoadElementsByIntentionSystem<EmotesResolution, GetOwnedEmotesFromRealmIntention, IEmote, EmoteDTO>
    {
        internal IURLBuilder urlBuilder = new URLBuilder();

        public LoadOwnedEmotesSystem(
            World world,
            IRealmData realmData,
            IWebRequestController webRequestController,
            IStreamableCache<EmotesResolution, GetOwnedEmotesFromRealmIntention> cache,
            IEmoteStorage emoteStorage,
            string? builderContentURL = null
        ) : base(world, cache, emoteStorage, webRequestController, realmData, builderContentURL, "emote")
        {
        }

        protected override async UniTask<IAttachmentLambdaResponse<ILambdaResponseElement<EmoteDTO>>> ParseResponseAsync(GenericGetRequest adapter, CancellationToken ct) =>
            await adapter.CreateFromJsonAsync<LambdaOwnedEmoteElementList>(WRJsonParser.Unity, ct);

        protected override async UniTask<IBuilderLambdaResponse<IBuilderLambdaResponseElement<EmoteDTO>>> ParseBuilderResponseAsync(GenericGetRequest adapter, CancellationToken ct) =>
            await adapter.CreateFromJsonAsync<BuilderEmoteDTO.BuilderLambdaResponse>(WRJsonParser.Newtonsoft, ct);

        protected override Uri BuildUrlFromIntention(in GetOwnedEmotesFromRealmIntention intention)
        {
            if (intention.CommonArguments.URL != URLAddress.EMPTY && intention.NeedsBuilderAPISigning)
            {
                urlBuilder.Clear();
                Uri url = intention.CommonArguments.URL;
                urlBuilder.AppendDomain(URLDomain.FromString($"{url.Scheme}://{url.Host}"))
                          .AppendSubDirectory(URLSubdirectory.FromString(url.AbsolutePath));
                return urlBuilder.Build();
            }

            return intention.CommonArguments.URL;
        }

        protected override EmotesResolution AssetFromPreparedIntention(in GetOwnedEmotesFromRealmIntention intention)
        {
            // Promise creation for builder collections is handled at ResolveBuilderEmotePromisesSystem
            return new EmotesResolution(intention.Result, intention.TotalAmount);
        }
    }
}
