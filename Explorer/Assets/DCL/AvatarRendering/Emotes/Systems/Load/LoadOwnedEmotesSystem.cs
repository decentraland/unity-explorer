using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.Systems.Abstract;
using DCL.Diagnostics;
using DCL.WebRequests;
using ECS;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.GLTF;
using System;
using GltfPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.GLTF.GLTFData, ECS.StreamableLoading.GLTF.GetGLTFIntention>;

namespace DCL.AvatarRendering.Emotes.Load
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.EMOTE)]
    public partial class LoadOwnedEmotesSystem : LoadElementsByIntentionSystem<EmotesResolution, GetOwnedEmotesFromRealmIntention, IEmote, EmoteDTO>
    {
        internal IURLBuilder urlBuilder = new URLBuilder();
        private readonly IEmoteStorage emoteStorage;

        public LoadOwnedEmotesSystem(
            World world,
            IRealmData realmData,
            IWebRequestController webRequestController,
            IStreamableCache<EmotesResolution, GetOwnedEmotesFromRealmIntention> cache,
            IEmoteStorage emoteStorage,
            string? builderContentURL = null
        ) : base(world, cache, emoteStorage, webRequestController, realmData, builderContentURL)
        {
            this.emoteStorage = emoteStorage;
        }

        protected override async UniTask<IAttachmentLambdaResponse<ILambdaResponseElement<EmoteDTO>>> ParseResponseAsync(GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> adapter) =>
            await adapter.CreateFromJson<LambdaOwnedEmoteElementList>(WRJsonParser.Unity);

        protected override async UniTask<IBuilderLambdaResponse<IBuilderLambdaResponseElement<EmoteDTO>>> ParseBuilderResponseAsync(GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> adapter) =>
            await adapter.CreateFromJson<BuilderEmoteDTO.BuilderLambdaResponse>(WRJsonParser.Newtonsoft);

        protected override URLAddress BuildUrlFromIntention(in GetOwnedEmotesFromRealmIntention intention)
        {
            if (intention.CommonArguments.URL != URLAddress.EMPTY && intention.NeedsBuilderAPISigning)
            {
                urlBuilder.Clear();
                var url = new Uri(intention.CommonArguments.URL);
                urlBuilder.AppendDomain(URLDomain.FromString($"{url.Scheme}://{url.Host}"))
                          .AppendSubDirectory(URLSubdirectory.FromString(url.AbsolutePath));
                return urlBuilder.Build();
            }

            return intention.CommonArguments.URL;
        }

        protected override EmotesResolution AssetFromPreparedIntention(in GetOwnedEmotesFromRealmIntention intention)
        {
            // Create asset promises for builder collection emotes after DTO loading is complete
            if (intention.NeedsBuilderAPISigning)
            {
                CreateAssetPromisesForBuilderEmotes(in intention);
            }

            return new EmotesResolution(intention.Result, intention.TotalAmount);
        }

        private void CreateAssetPromisesForBuilderEmotes(in GetOwnedEmotesFromRealmIntention intention)
        {
            foreach (IEmote emote in intention.Result.List)
            {
                TryCreateRawEmoteGltfPromise(emote);
            }
        }

        private bool TryCreateRawEmoteGltfPromise(IEmote emote)
        {
            BodyShape bodyShape = BodyShape.MALE;

            // Check if emote already has assets loaded
            if (emote.AssetResults[bodyShape] != null)
                return false;

            if (emote.IsLoading)
                return false;

            if (string.IsNullOrEmpty(emote.DTO.ContentDownloadUrl))
                return false;

            // Check if we already have this emote in storage with assets
            if (emoteStorage.TryGetElement(emote.GetUrn(), out IEmote existingEmote))
            {
                if (existingEmote.AssetResults[bodyShape] != null)
                    return false;
            }

            foreach (var content in emote.DTO.content)
            {
                if (content.file.EndsWith(".glb"))
                {
                    var promise = GltfPromise.Create(World, GetGLTFIntention.Create(content.file, content.hash), PartitionComponent.TOP_PRIORITY);
                    World.Create(promise, emote, bodyShape, 0);

                    emote.UpdateLoadingStatus(true);
                    return true;
                }
            }
            return false;
        }
    }
}
