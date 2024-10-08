using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using DCL.SDKComponents.AudioSources;
using ECS;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.GLTF;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;
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
        private readonly IRealmData realmData;
        private readonly URLBuilder urlBuilder;

        public LoadSceneEmotesSystem(
            World world,
            IEmoteStorage emoteStorage,
            IRealmData realmData,
            URLSubdirectory customStreamingSubdirectory
        )
            : base(world)
        {
            this.emoteStorage = emoteStorage;
            this.customStreamingSubdirectory = customStreamingSubdirectory;
            this.realmData = realmData;
            urlBuilder = new URLBuilder();
        }

        protected override void Update(float t)
        {
            GetEmotesFromRealmQuery(World, t);
            GetEmotesByPointersQuery(World, t);
            GetEmotesFromRealmLSDQuery(World, t);
        }

        // TODO: this query should not be in this system. This system should only process scene emotes, but this query is processing emotes of avatars
        [Query]
        [None(typeof(StreamableResult))]
        private void GetEmotesFromRealmLSD([Data] float dt, in Entity entity,
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

            if (CreateGltfPromiseIfRequired(emote, in intention, partitionComponent)) return;

            //List<AttachmentRegularAsset.RendererInfo> rendererInfos = AttachmentRegularAsset.RENDERER_INFO_POOL.Get();
            //
            // emote.AssetResults[intention.BodyShape] =
            //     new StreamableLoadingResult<AttachmentRegularAsset>(
            //         new AttachmentRegularAsset(intention.gltfRoot, rendererInfos,null));

            //emote.AssetResults[intention.BodyShape]?.Asset!.AddReference();

            //World.Add(entity, new StreamableResult(new EmotesResolution(RepoolableList<IEmote>.FromElement(emote), 1)));
        }

        private bool CreateGltfPromiseIfRequired(IEmote emote, in GetSceneEmoteFromLocalDevelopmentSceneIntention intention, IPartitionComponent partitionComponent)
        {
            if (emote.AssetResults[intention.BodyShape] != null) return false;

            // The resolution of the GltfPromise will be finalized by ??
            var promise = GltfPromise.Create(World,
                GetGLTFIntention.Create(intention.SceneData, intention.EmotePath, intention.EmoteHash),
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

        [Query]
        [None(typeof(StreamableResult))]
        private void GetEmotesByPointers([Data] float dt, in Entity entity,
            ref GetEmotesByPointersIntention intention,
            ref IPartitionComponent partitionComponent)
        {
            if (intention.TryCancelByRequest<GetEmotesByPointersIntention, EmotesResolution>(
                    World!,
                    GetReportCategory(),
                    entity,
                    static _ => "Pointer request cancelled"))
                return;

            using var _ = ListPool<URN>.Get(out var pointersToRequest)!;
            var resolvedEmotesTmp = RepoolableList<IEmote>.NewList();

            ExtractMissingPointersAndResolvedEmotes(in intention, pointersToRequest!, resolvedEmotesTmp);

            if (intention.Timeout.IsTimeout(dt))
            {
                var pointersStrLog = string.Join(",", intention.Pointers);
                ReportHub.LogWarning(GetReportCategory(), $"Loading emotes timed out, {pointersStrLog}");

                ResolveIntentionWithSuccessfulEmotes(entity, intention, resolvedEmotesTmp);

                return;
            }

            if (RequestMissingPointers(pointersToRequest!, partitionComponent, intention.BodyShape)) return;

            bool success = GetAssetBundlesUntilAllAreResolved(in intention, partitionComponent, resolvedEmotesTmp.List);

            if (success)
                World!.Add(entity, NewEmotesResult(resolvedEmotesTmp, intention.Pointers.Count));
        }

        private void ResolveIntentionWithSuccessfulEmotes(Entity entity,
            GetEmotesByPointersIntention intention,
            RepoolableList<IEmote> resolvedEmotesTmp)
        {
            HashSet<URN> successfulPointers = intention.SuccessfulPointers;
            // Keep only successful emotes in the result list
            resolvedEmotesTmp.List.RemoveAll(emote => !successfulPointers.Contains(emote.GetUrn()));

            World.Add(entity, new StreamableResult(new EmotesResolution(resolvedEmotesTmp, resolvedEmotesTmp.List.Count)));
        }

        private static StreamableResult NewEmotesResult(RepoolableList<IEmote> resolvedEmotesTmp, int pointersCount) =>
            new (new EmotesResolution(resolvedEmotesTmp, pointersCount));

        private bool GetAssetBundlesUntilAllAreResolved(
            in GetEmotesByPointersIntention intention,
            IPartitionComponent partitionComponent,
            IEnumerable<IEmote> emotes
        )
        {
            var emotesWithResponse = 0;

            foreach (IEmote emote in emotes)
            {
                if (emote.ManifestResult is { Exception: not null })
                    emotesWithResponse++;

                if (emote.IsLoading) continue;
                if (CreateAssetBundlePromiseIfRequired(emote, in intention, partitionComponent)) continue;

                if (emote.AssetResults[intention.BodyShape] != null)

                    // TODO: it may occur that the requested emote does not support the body shape
                    // If that is the case, the promise will never be resolved
                    emotesWithResponse++;

                if (emote.AssetResults[intention.BodyShape] is { Succeeded: true })

                    // Reference must be added only once when the wearable is resolved
                    if (!intention.SuccessfulPointers.Contains(emote.GetUrn()))
                    {
                        intention.SuccessfulPointers.Add(emote.GetUrn());

                        // We need to add a reference here, so it is not lost if the flow interrupts in between (i.e. before creating instances of CachedWearable)
                        emote.AssetResults[intention.BodyShape]?.Asset?.AddReference();
                    }
            }

            return emotesWithResponse == intention.Pointers.Count;
        }

        private bool RequestMissingPointers(ICollection<URN> missingPointers, IPartitionComponent partitionComponent, BodyShape forBodyShape)
        {
            if (missingPointers.Count <= 0) return false;

            var promise = EmotesFromRealmPromise.Create(
                World!,
                new GetEmotesByPointersFromRealmIntention(missingPointers.ToList(),
                    new CommonLoadingArguments(realmData.Ipfs.EntitiesActiveEndpoint)
                ),
                partitionComponent
            );

            World!.Create(promise, forBodyShape, partitionComponent);

            return true;
        }

        private void ExtractMissingPointersAndResolvedEmotes(
            in GetEmotesByPointersIntention intention,
            ICollection<URN> missingPointers,
            RepoolableList<IEmote> resolvedEmotes
        )
        {
            foreach (URN loadingIntentionPointer in intention.Pointers)
            {
                if (loadingIntentionPointer.IsNullOrEmpty())
                {
                    ReportHub.LogError(
                        GetReportCategory(),
                        "ResolveWearableByPointerSystem: Null pointer found in the list of pointers"
                    );

                    continue;
                }

                URN shortenedPointer = loadingIntentionPointer.Shorten();

                if (!emoteStorage.TryGetElement(shortenedPointer, out IEmote emote))
                {
                    if (!intention.RequestedPointers.Contains(loadingIntentionPointer))
                    {
                        missingPointers.Add(shortenedPointer);
                        intention.RequestedPointers.Add(loadingIntentionPointer);
                    }

                    continue;
                }

                if (emote.Model.Succeeded)
                    resolvedEmotes.List.Add(emote);
            }
        }

        private bool CreateAssetBundlePromiseIfRequired(IEmote component, in GetEmotesByPointersIntention intention, IPartitionComponent partitionComponent)
        {
            // Manifest is required for Web loading only
            if (component.ManifestResult == null
                && EnumUtils.HasFlag(intention.PermittedSources, AssetSource.WEB)

                // Skip processing manifest for embedded emotes which do not start with 'urn'
                && component.GetUrn().IsValid())

                // The resolution of the AB promise will be finalized by FinalizeEmoteAssetBundleSystem
                return component.CreateAssetBundleManifestPromise(World!, intention.BodyShape, intention.CancellationTokenSource, partitionComponent);

            if (!component.TryGetMainFileHash(intention.BodyShape, out string? hash))
                return false;

            if (component.AssetResults[intention.BodyShape] == null)
            {
                SceneAssetBundleManifest? manifest = !EnumUtils.HasFlag(intention.PermittedSources, AssetSource.WEB) ? null : component.ManifestResult?.Asset;

                // The resolution of the AB promise will be finalized by FinalizeEmoteAssetBundleSystem
                var promise = AssetBundlePromise.Create(
                    World!,
                    GetAssetBundleIntention.FromHash(
                        typeof(GameObject),
                        hash! + PlatformUtils.GetCurrentPlatform(),
                        permittedSources: intention.PermittedSources,
                        customEmbeddedSubDirectory: customStreamingSubdirectory,
                        manifest: manifest,
                        cancellationTokenSource: intention.CancellationTokenSource
                    ),
                    partitionComponent
                );

                TryCreateAudioClipPromises(component, intention.BodyShape, partitionComponent);

                component.UpdateLoadingStatus(true);
                World!.Create(promise, component, intention.BodyShape);
                return true;
            }

            return false;
        }

        private void TryCreateAudioClipPromises(IEmote component, BodyShape bodyShape, IPartitionComponent partitionComponent)
        {
            AvatarAttachmentDTO.Content[]? content = component.Model.Asset!.content;

            foreach (AvatarAttachmentDTO.Content item in content ?? Array.Empty<AvatarAttachmentDTO.Content>())
            {
                var audioType = item.file.ToAudioType();

                if (audioType == AudioType.UNKNOWN)
                    continue;

                urlBuilder.Clear();
                urlBuilder.AppendDomain(realmData.Ipfs.ContentBaseUrl).AppendPath(new URLPath(item.hash));
                URLAddress url = urlBuilder.Build();

                // The resolution of the audio promise will be finalized by FinalizeEmoteAssetBundleSystem
                AudioPromise promise = AudioUtils.CreateAudioClipPromise(World!, url.Value, audioType, partitionComponent);
                World!.Create(promise, component, bodyShape);
            }
        }
    }
}
