using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Loading.Systems.Abstract;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using DCL.SDKComponents.AudioSources;
using DCL.WebRequests;
using ECS;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using Global.AppArgs;
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
    public partial class LoadEmotesByPointersSystem : LoadElementsByPointersSystem<EmotesDTOList, GetEmotesByPointersFromRealmIntention, EmoteDTO>
    {
        private readonly IEmoteStorage emoteStorage;
        private readonly IRealmData realmData;
        private readonly URLSubdirectory customStreamingSubdirectory;
        private readonly URLBuilder urlBuilder = new ();
        private readonly bool builderEmotesPreview;

        public LoadEmotesByPointersSystem(
            World world,
            IWebRequestController webRequestController,
            IStreamableCache<EmotesDTOList, GetEmotesByPointersFromRealmIntention> cache,
            IEmoteStorage emoteStorage,
            IRealmData realmData,
            URLSubdirectory customStreamingSubdirectory,
            IAppArgs appArgs
        )
            : base(world, cache, webRequestController)
        {
            this.emoteStorage = emoteStorage;
            this.realmData = realmData;
            this.customStreamingSubdirectory = customStreamingSubdirectory;
            this.builderEmotesPreview = appArgs.HasFlag(AppArgsFlags.SELF_PREVIEW_BUILDER_EMOTE_COLLECTIONS);
        }

        protected override EmotesDTOList CreateAssetFromListOfDTOs(RepoolableList<EmoteDTO> list) =>
            new (list);

        protected override void Update(float t)
        {
            base.Update(t);

            GetEmotesByPointersQuery(World, t);
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

            bool success = builderEmotesPreview
                ? GetGltfsUntilAllAreResolved(in intention, partitionComponent, resolvedEmotesTmp.List)
                : GetAssetBundlesUntilAllAreResolved(in intention, partitionComponent, resolvedEmotesTmp.List);

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

        private bool GetGltfsUntilAllAreResolved(
            in GetEmotesByPointersIntention intention,
            IPartitionComponent partitionComponent,
            IEnumerable<IEmote> emotes
        )
        {
            var emotesWithResponse = 0;

            foreach (IEmote emote in emotes)
            {
                var urn = emote.GetUrn();
                bool debug = urn.Equals("0b9d4454-8c03-4be5-acb3-df48baa94ad3")
                             || urn.Equals("946e0c52-7406-432f-93fd-e7d9f0b329b8")
                             || urn.Equals("9cee1007-1f7a-4ee2-ac9b-8c50cbbf6193");

                if (debug)
                {
                    Debug.Log($"PRAVS - GetGltfsUntilAllAreResolved() - Checking emote: {emote.GetUrn()}");
                    Debug.Log($"PRAVS - GetGltfsUntilAllAreResolved() - IsLoading: {emote.IsLoading}");
                    Debug.Log($"PRAVS - GetGltfsUntilAllAreResolved() - AssetResults[{intention.BodyShape}]: {emote.AssetResults[intention.BodyShape]}");
                }

                // In builder emote collections mode, skip non-builder emotes that can't be resolved
                if (IsNonBuilderEmoteThatCantBeResolved(emote, intention.BodyShape))
                {
                    if (debug)
                        Debug.Log($"PRAVS - GetGltfsUntilAllAreResolved() - Skipping non-builder emote {emote.GetUrn()} in builder collections mode");
                    emotesWithResponse++;
                    continue;
                }

                if (emote.IsLoading)
                {
                    if (debug)
                        Debug.Log($"PRAVS - GetGltfsUntilAllAreResolved() - Emote {emote.GetUrn()} is loading, continuing");
                    continue;
                }

                // Check if this emote is currently loading via GLTF promise (for builder emotes)
                if (IsEmoteLoadingViaGltfPromise(emote, intention.BodyShape))
                {
                    if (debug)
                        Debug.Log($"PRAVS - GetGltfsUntilAllAreResolved() - Emote {emote.GetUrn()} is loading via GLTF promise, continuing");
                    continue;
                }

                if (emote.AssetResults[intention.BodyShape] != null)
                {
                    if (debug)
                        Debug.Log($"PRAVS - GetGltfsUntilAllAreResolved() - Emote {emote.GetUrn()} has asset result, counting as resolved");
                    emotesWithResponse++;
                }
                else
                {
                    if (debug)
                        Debug.Log($"PRAVS - GetGltfsUntilAllAreResolved() - Emote {emote.GetUrn()} has no asset result and no promise created");
                }

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

        /// <summary>
        /// Checks if an emote is currently loading via GLTF promise (for builder emotes)
        /// </summary>
        private bool IsEmoteLoadingViaGltfPromise(IEmote emote, BodyShape bodyShape)
        {
            bool isLoading = false;

            // Query all entities with GltfPromise and IEmote components
            World.Query(in new QueryDescription().WithAll<GltfPromise, IEmote, BodyShape>(),
                (Entity entity, ref GltfPromise promise, ref IEmote promiseEmote) =>
                {
                    // Query() doesn't accept more than 3 parameters in its forEach delegate...
                    var promiseBodyShape = World.Get<BodyShape>(entity);

                    // Check if this promise is for the same emote and body shape
                    if (promiseEmote.GetUrn().Equals(emote.GetUrn()) && promiseBodyShape.Equals(bodyShape))
                    {
                        // Check if the promise is still active (not consumed and not cancelled)
                        if (!promise.IsConsumed && !promise.IsCancellationRequested(World))
                        {
                            Debug.Log($"PRAVS - IsEmoteLoadingViaGltfPromise() - Found active GLTF promise for emote: {emote.GetUrn()}");
                            isLoading = true;
                        }
                    }
                });

            return isLoading;
        }

        /// <summary>
        /// Checks if an emote is a non-builder emote that can't be resolved in builder collections mode
        /// </summary>
        private bool IsNonBuilderEmoteThatCantBeResolved(IEmote emote, BodyShape bodyShape)
        {
            // If the emote already has an asset result, it's resolved
            if (emote.AssetResults[bodyShape] != null)
                return false;

            // If the emote is currently loading via GLTF (builder emote), don't skip it
            if (IsEmoteLoadingViaGltfPromise(emote, bodyShape))
                return false;

            // If the emote is currently loading via other means, don't skip it
            if (emote.IsLoading)
                return false;

            // If we can't get the main file hash, it's likely a non-builder emote that can't be resolved
            if (!emote.TryGetMainFileHash(bodyShape, out string? hash))
                return true;

            // If the emote has a manifest result with an exception, it failed to load
            if (emote.ManifestResult is { Exception: not null })
                return false; // Don't skip, let it count as resolved with error

            // If we reach here, it's likely a regular emote that would normally be loaded via AssetBundle
            // In builder collections mode, we want to skip these to avoid timeout
            return true;
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

                // Debug.Log($"PRAVS - LoadEmotesByPointersSystem.ExtractMissingPointersAndResolvedEmotes() - pointer: {loadingIntentionPointer}");

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
