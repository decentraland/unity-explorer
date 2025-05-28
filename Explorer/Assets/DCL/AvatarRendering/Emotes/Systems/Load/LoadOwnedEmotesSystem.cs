using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.Systems.Abstract;
using DCL.Diagnostics;
using DCL.SDKComponents.AudioSources;
using DCL.WebRequests;
using ECS;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.GLTF;
using System;
using AudioPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AudioClips.AudioClipData, ECS.StreamableLoading.AudioClips.GetAudioClipIntention>;
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
                foreach (IEmote emote in intention.Result.List)
                {
                    TryCreateBuilderEmoteAssetPromises(emote);
                }
            }

            return new EmotesResolution(intention.Result, intention.TotalAmount);
        }

        private bool TryCreateBuilderEmoteAssetPromises(IEmote emote)
        {
            BodyShape bodyShape = BodyShape.MALE;

            if (string.IsNullOrEmpty(emote.DTO.ContentDownloadUrl))
                return false;

            // Check if emote already has assets loaded or is loading
            if (emote.AssetResults[bodyShape] != null || emote.IsLoading)
                return false;

            // Check if we already have this emote in storage with assets
            if (emoteStorage.TryGetElement(emote.GetUrn(), out IEmote existingEmote))
            {
                if (existingEmote.AssetResults[bodyShape] != null)
                    return false;
            }

            bool foundGlb = false;

            // The resolution of these promises will be finalized by FinalizeEmoteLoadingSystem
            foreach (var content in emote.DTO.content)
            {
                if (content.file.EndsWith(".glb"))
                {
                    var gltfPromise = GltfPromise.Create(World, GetGLTFIntention.Create(content.file, content.hash), PartitionComponent.TOP_PRIORITY);
                    World.Create(gltfPromise, emote, bodyShape);
                    emote.UpdateLoadingStatus(true);
                    foundGlb = true;
                    continue;
                }

                // Supported audio format in emotes: https://docs.decentraland.org/creator/emotes/props-and-sounds/#add-audio-to-the-emotes
                if (content.file.EndsWith(".mp3") || content.file.EndsWith(".ogg"))
                {
                    var audioType = content.file.ToAudioType();
                    urlBuilder.Clear();
                    urlBuilder.AppendDomain(URLDomain.FromString(emote.DTO.ContentDownloadUrl)).AppendPath(new URLPath(content.hash));
                    URLAddress url = urlBuilder.Build();

                    var audioPromise = AudioUtils.CreateAudioClipPromise(World, url.Value, audioType, PartitionComponent.TOP_PRIORITY);
                    World.Create(audioPromise, emote, bodyShape);

                    if (foundGlb) break;
                }
            }

            return foundGlb;
        }
    }
}
