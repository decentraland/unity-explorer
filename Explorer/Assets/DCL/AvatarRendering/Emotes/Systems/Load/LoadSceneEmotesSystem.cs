using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.GLTF;
using SceneRunner.Scene;
using System;
using UnityEngine;
using Utility;
using StreamableResult = ECS.StreamableLoading.Common.Components.StreamableLoadingResult<DCL.AvatarRendering.Emotes.EmotesResolution>;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;
using EmotesFromRealmPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesDTOList, DCL.AvatarRendering.Emotes.GetEmotesByPointersFromRealmIntention>;
using AudioPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AudioClips.AudioClipData, ECS.StreamableLoading.AudioClips.GetAudioClipIntention>;
using GltfPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.GLTF.GLTFData, ECS.StreamableLoading.GLTF.GetGLTFIntention>;

namespace DCL.AvatarRendering.Emotes.Load
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.EMOTE)]
    public partial class LoadSceneEmotesSystem : BaseUnityLoopSystem
    {
        private readonly URLSubdirectory customStreamingSubdirectory;
        private readonly IEmoteStorage emoteStorage;

        public LoadSceneEmotesSystem(
            World world,
            IEmoteStorage emoteStorage,
            URLSubdirectory customStreamingSubdirectory
        )
            : base(world)
        {
            this.emoteStorage = emoteStorage;
            this.customStreamingSubdirectory = customStreamingSubdirectory;
        }

        protected override void Update(float t)
        {
            GetEmotesFromRealmQuery(World, t);
            GetEmotesFromLocalSceneQuery(World, t);
        }

        [Query]
        [None(typeof(StreamableResult))]
        private void GetEmotesFromLocalScene([Data] float dt, in Entity entity,
            ref GetSceneEmoteFromLocalDevelopmentSceneIntention intention,
            ref IPartitionComponent partitionComponent)
        {
            URN urn = intention.NewSceneEmoteURN();

            if (intention.Timeout.IsTimeout(dt))
            {
                if (!World.Has<StreamableResult>(entity))
                {
                    ReportHub.LogWarning(GetReportCategory(), $"Loading scenes emotes timed out {urn}");
                    World.Add(entity, new StreamableResult(GetReportCategory(), new TimeoutException($"Scene emote timeout {urn}")));
                }

                return;
            }

            if (!emoteStorage.TryGetElement(urn, out IEmote emote))
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
                                    mainFile = string.Empty,
                                },
                            },
                        },
                    },
                };

                emote = emoteStorage.GetOrAddByDTO(dto);
            }

            if (emote.IsLoading) return;

            if (CreateGltfPromiseIfRequired(emote, in intention, partitionComponent)) return;

            if (emote.AssetResults[intention.BodyShape] is { Succeeded: true })
            {
                // We need to add a reference here, so it is not lost if the flow interrupts in between (i.e. before creating instances of CachedWearable)
                emote.AssetResults[intention.BodyShape]?.Asset!.AddReference();
            }
            else
            {
                // TODO check if we really need to do this
                World.Add(entity, new StreamableResult(GetReportCategory(), new Exception($"Scene emote failed to load {urn}")));
                return;
            }

            World.Add(entity, new StreamableResult(new EmotesResolution(RepoolableList<IEmote>.FromElement(emote), 1)));
        }

        private bool CreateGltfPromiseIfRequired(IEmote emote, in GetSceneEmoteFromLocalDevelopmentSceneIntention intention, IPartitionComponent partitionComponent)
        {
            if (emote.AssetResults[intention.BodyShape] != null) return false;

            // The resolution of the GltfPromise will be finalized by ??
            var promise = GltfPromise.Create(World,
                GetGLTFIntention.Create(intention.SceneData, intention.EmotePath, intention.EmoteHash, true),
                partitionComponent);

            emote.UpdateLoadingStatus(true);
            World.Create(promise, emote, intention.BodyShape);

            return true;
        }

        [Query]
        [None(typeof(StreamableResult))]
        private void GetEmotesFromRealm([Data] float dt, in Entity entity,
            ref GetSceneEmoteFromRealmIntention intention,
            ref IPartitionComponent partitionComponent)
        {
            if (intention.TryCancelByRequest<GetSceneEmoteFromRealmIntention, EmotesResolution>(
                    World!,
                    GetReportCategory(),
                    entity,
                    static i => $"Scene emote request cancelled {i.EmoteHash}"))
                return;

            URN urn = intention.NewSceneEmoteURN();

            if (intention.Timeout.IsTimeout(dt))
            {
                if (!World.Has<StreamableResult>(entity))
                {
                    ReportHub.LogWarning(GetReportCategory(), $"Loading scenes emotes timed out {urn}");
                    World.Add(entity, new StreamableResult(GetReportCategory(), new TimeoutException($"Scene emote timeout {urn}")));
                }

                return;
            }

            if (!emoteStorage.TryGetElement(urn, out IEmote emote))
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
                                    mainFile = string.Empty,
                                },
                            },
                        },
                    },
                };

                emote = emoteStorage.GetOrAddByDTO(dto);
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

            World.Add(entity, new StreamableResult(new EmotesResolution(RepoolableList<IEmote>.FromElement(emote), 1)));
        }

        private bool CreateAssetBundlePromiseIfRequired(IEmote emote, in GetSceneEmoteFromRealmIntention intention, IPartitionComponent partitionComponent)
        {
            if (emote.AssetResults[intention.BodyShape] != null) return false;

            // The resolution of the AB promise will be finalized by FinalizeEmoteAssetBundleSystem
            var promise = AssetBundlePromise.Create(World,
                GetAssetBundleIntention.FromHash(typeof(GameObject),
                    intention.EmoteHash + PlatformUtils.GetCurrentPlatform(),
                    permittedSources: intention.PermittedSources,
                    customEmbeddedSubDirectory: customStreamingSubdirectory,
                    cancellationTokenSource: intention.CancellationTokenSource,
                    manifest: intention.AssetBundleManifest),
                partitionComponent);

            emote.UpdateLoadingStatus(true);
            World.Create(promise, emote, intention.BodyShape);

            return true;
        }
    }
}
