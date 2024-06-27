using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using System;
using UnityEngine;
using Utility;
using StreamableResult = ECS.StreamableLoading.Common.Components.StreamableLoadingResult<DCL.AvatarRendering.Emotes.EmotesResolution>;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.AvatarRendering.Emotes
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.EMOTE)]
    public partial class LoadSceneEmotesSystem : BaseUnityLoopSystem
    {
        private const string SCENE_EMOTE_PREFIX = "urn:decentraland:off-chain:scene-emote";

        private readonly URLSubdirectory customStreamingSubdirectory;
        private readonly IEmoteCache emoteCache;

        public LoadSceneEmotesSystem(World world,
            IEmoteCache emoteCache,
            URLSubdirectory customStreamingSubdirectory)
            : base(world)
        {
            this.emoteCache = emoteCache;
            this.customStreamingSubdirectory = customStreamingSubdirectory;
        }

        protected override void Update(float t)
        {
            GetEmotesFromRealmQuery(World, t);
        }

        [Query]
        [None(typeof(StreamableResult))]
        private void GetEmotesFromRealm([Data] float dt, in Entity entity,
            ref GetSceneEmoteFromRealmIntention intention,
            ref IPartitionComponent partitionComponent)
        {
            if (intention.CancellationTokenSource.IsCancellationRequested)
            {
                if (!World.Has<StreamableResult>(entity))
                    World.Add(entity, new StreamableResult(new OperationCanceledException($"Scene emote request cancelled {intention.Hash}")));

                return;
            }

            intention.ElapsedTime += dt;

            URN urn = GetUrn(intention.Hash, intention.Loop);

            bool isTimeout = intention.ElapsedTime >= intention.Timeout;

            if (isTimeout)
            {
                if (!World.Has<StreamableResult>(entity))
                {
                    ReportHub.LogWarning(GetReportCategory(), $"Loading scenes emotes timed out {urn}");
                    World.Add(entity, new StreamableResult(new TimeoutException($"Scene emote timeout {urn}")));
                }

                return;
            }

            if (!emoteCache.TryGetEmote(urn, out IEmote emote))
            {
                var dto = new EmoteDTO
                {
                    id = urn,
                    metadata = new EmoteDTO.Metadata
                    {
                        id = urn,
                        emoteDataADR74 = new EmoteDTO.Metadata.Data
                        {
                            loop = intention.Loop,
                            category = "emote",
                            hides = Array.Empty<string>(),
                            replaces = Array.Empty<string>(),
                            tags = Array.Empty<string>(),
                            removesDefaultHiding = Array.Empty<string>(),
                            representations = new AvatarAttachmentDTO.Representation[]
                            {
                                new ()
                                {
                                    contents = Array.Empty<string>(),
                                    bodyShapes = new[]
                                    {
                                        BodyShape.MALE.Value,
                                        BodyShape.FEMALE.Value,
                                    },
                                    overrideHides = Array.Empty<string>(),
                                    overrideReplaces = Array.Empty<string>(),
                                    mainFile = "",
                                },
                            },
                        },
                    },
                };

                emote = emoteCache.GetOrAddEmoteByDTO(dto);
            }

            if (emote.IsLoading) return;
            if (CreateAssetBundlePromiseIfRequired(emote, in intention, partitionComponent)) return;

            if (emote.AssetResults[intention.BodyShape] != null && !intention.IsAssetBundleProcessed)
            {
                // TODO: it may occur that the requested emote does not support the body shape
                // If that is the case, the promise will never be resolved
                intention.IsAssetBundleProcessed = true;

                if (emote.AssetResults[intention.BodyShape] is { Succeeded: true })
                {
                    // We need to add a reference here, so it is not lost if the flow interrupts in between (i.e. before creating instances of CachedWearable)
                    emote.AssetResults[intention.BodyShape]?.Asset!.AddReference();
                }
            }

            if (!intention.IsAssetBundleProcessed) return;

            World.Add(entity, new StreamableResult(new EmotesResolution(new[] { emote }, 1)));
        }

        private bool CreateAssetBundlePromiseIfRequired(IEmote emote, in GetSceneEmoteFromRealmIntention intention, IPartitionComponent partitionComponent)
        {
            if (emote.AssetResults[intention.BodyShape] != null) return false;

            // The resolution of the AB promise will be finalized by FinalizeEmoteAssetBundleSystem
            var promise = AssetBundlePromise.Create(World,
                GetAssetBundleIntention.FromHash(typeof(GameObject),
                    intention.Hash + PlatformUtils.GetPlatform(),
                    permittedSources: intention.PermittedSources,
                    customEmbeddedSubDirectory: customStreamingSubdirectory,
                    cancellationTokenSource: intention.CancellationTokenSource,
                    manifest: intention.AssetBundleManifest),
                partitionComponent);

            emote.IsLoading = true;
            World.Create(promise, emote, intention.BodyShape);

            return true;
        }

        private static URN GetUrn(string hash, bool loop) =>
            new ($"{SCENE_EMOTE_PREFIX}:{hash}-{loop.ToString().ToLower()}");
    }
}
