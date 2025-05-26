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
using UnityEngine;
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

        protected override EmotesResolution AssetFromPreparedIntention(in GetOwnedEmotesFromRealmIntention intention)
        {
            // Create asset promises for builder collection emotes after DTO loading is complete
            if (intention.NeedsBuilderAPISigning)
            {
                CreateAssetPromisesForBuilderEmotes(in intention);
            }

            return new EmotesResolution(intention.Result, intention.TotalAmount);
        }

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

        private void CreateAssetPromisesForBuilderEmotes(in GetOwnedEmotesFromRealmIntention intention)
        {
            Debug.Log($"PRAVS - LoadOwnedEmotesSystem.CreateAssetPromisesForBuilderEmotes() - Processing {intention.Result.List.Count} builder emotes");

            foreach (IEmote emote in intention.Result.List)
            {
                if (TryCreateRawEmoteGltfPromise(emote))
                {
                    Debug.Log($"PRAVS - LoadOwnedEmotesSystem - Created GLTF promise for builder emote: {emote.GetUrn()}");
                }
            }
        }

        private bool TryCreateRawEmoteGltfPromise(IEmote emote)
        {
            BodyShape bodyShape = BodyShape.MALE;

            // Check if emote already has assets loaded
            if (emote.AssetResults[bodyShape] != null)
            {
                Debug.Log($"PRAVS - LoadOwnedEmotesSystem.TryCreateRawEmoteGltfPromise() - Emote {emote.GetUrn()} already has assets loaded, skipping");
                return false;
            }

            // Check if emote is already being loaded
            if (emote.IsLoading)
            {
                Debug.Log($"PRAVS - LoadOwnedEmotesSystem.TryCreateRawEmoteGltfPromise() - Emote {emote.GetUrn()} is already loading, skipping");
                return false;
            }

            // Check if this is a builder collection emote with ContentDownloadUrl
            if (string.IsNullOrEmpty(emote.DTO.ContentDownloadUrl))
            {
                Debug.Log($"PRAVS - LoadOwnedEmotesSystem.TryCreateRawEmoteGltfPromise() - Emote {emote.GetUrn()} has no ContentDownloadUrl, skipping");
                return false;
            }

            // Check if we already have this emote in storage with assets
            if (emoteStorage.TryGetElement(emote.GetUrn(), out IEmote existingEmote))
            {
                if (existingEmote.AssetResults[bodyShape] != null)
                {
                    Debug.Log($"PRAVS - LoadOwnedEmotesSystem.TryCreateRawEmoteGltfPromise() - Emote {emote.GetUrn()} already exists in storage with assets, skipping");
                    return false;
                }
            }

            foreach (var content in emote.DTO.content)
            {
                if (content.file.EndsWith(".glb"))
                {
                    Debug.Log($"PRAVS - LoadOwnedEmotesSystem.TryCreateRawEmoteGltfPromise() - Creating GLTF promise for builder emote: {emote.GetUrn()}, file: {content.file}");

                    var promise = GltfPromise.Create(World, GetGLTFIntention.Create(content.file, content.hash), PartitionComponent.TOP_PRIORITY);
                    World.Create(promise, emote, bodyShape, 0);

                    emote.UpdateLoadingStatus(true);
                    return true;
                }
            }

            Debug.Log($"PRAVS - LoadOwnedEmotesSystem.TryCreateRawEmoteGltfPromise() - Emote {emote.GetUrn()} has no .glb content, skipping");
            return false;
        }
    }
}
