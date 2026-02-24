using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.Systems.Abstract;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.SDKComponents.AudioSources;
using DCL.Utility;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using StreamableResult = ECS.StreamableLoading.Common.Components.StreamableLoadingResult<DCL.AvatarRendering.Emotes.EmotesResolution>;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;
using EmotesFromRealmPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesDTOList, DCL.AvatarRendering.Emotes.GetEmotesDTOByPointersFromRealmIntention>;
using AudioPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AudioClips.AudioClipData, ECS.StreamableLoading.AudioClips.GetAudioClipIntention>;

namespace DCL.AvatarRendering.Emotes.Load
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.EMOTE)]
    public partial class LoadEmotesByPointersSystem : LoadElementsByPointersSystem<EmotesDTOList, GetEmotesDTOByPointersFromRealmIntention, EmoteDTO>
    {
        private readonly IEmoteStorage emoteStorage;
        private readonly IDecentralandUrlsSource urlsSource;
        private readonly URLSubdirectory customStreamingSubdirectory;
        private readonly URLBuilder urlBuilder = new ();

        public LoadEmotesByPointersSystem(
            World world,
            IWebRequestController webRequestController,
            IStreamableCache<EmotesDTOList, GetEmotesDTOByPointersFromRealmIntention> cache,
            IEmoteStorage emoteStorage,
            IDecentralandUrlsSource urlsSource,
            URLSubdirectory customStreamingSubdirectory,
            EntitiesAnalytics entitiesAnalytics
        )
            : base(world, cache, webRequestController, entitiesAnalytics)
        {
            this.emoteStorage = emoteStorage;
            this.urlsSource = urlsSource;
            this.customStreamingSubdirectory = customStreamingSubdirectory;
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

            using PooledObject<List<URN>> _ = WearableComponentsUtils.POINTERS_POOL.Get(out List<URN>? pointersToRequest)!;
            var resolvedEmotesTmp = RepoolableList<IEmote>.NewList();

            ExtractMissingPointersAndResolvedEmotes(in intention, pointersToRequest!, resolvedEmotesTmp.List);

            if (intention.IsTimeout(dt))
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
            else
                resolvedEmotesTmp.Dispose();
        }

        private void ResolveIntentionWithSuccessfulEmotes(Entity entity,
            GetEmotesByPointersIntention intention,
            RepoolableList<IEmote> resolvedEmotesTmp)
        {
            HashSet<URN> successfulPointers = intention.SuccessfulPointers;
            // Keep only successful emotes in the result list (also remove emotes with unresolved DTO)
            resolvedEmotesTmp.List.RemoveAll(emote => emote.DTO?.Metadata == null || !successfulPointers.Contains(emote.GetUrn()));

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
                // Skip emotes with unresolved DTO - treat as failed
                if (emote.DTO?.Metadata == null)
                {
                    emotesWithResponse++;
                    continue;
                }

                if (emote.DTO.assetBundleManifestVersion is { assetBundleManifestRequestFailed: true } || emote.Model is { Exception: not null })
                {
                    emotesWithResponse++;
                    continue;
                }

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

            List<URN> convertedPointers = WearableComponentsUtils.POINTERS_POOL.Get()!;

            foreach (URN pointer in missingPointers)
                convertedPointers.Add(EmoteComponentsUtils.ConvertLegacyEmoteUrnToOnChain(pointer));

            var promise = EmotesFromRealmPromise.Create(
                World!,
                new GetEmotesDTOByPointersFromRealmIntention(convertedPointers,
                    new CommonLoadingArguments(urlsSource.Url(DecentralandUrl.EntitiesActive))
                ),
                partitionComponent
            );

            World!.Create(promise, forBodyShape, partitionComponent);

            return true;
        }

        private void ExtractMissingPointersAndResolvedEmotes(
            in GetEmotesByPointersIntention intention,
            ICollection<URN> missingPointers,
            IList<IEmote> resolvedEmotes
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

                if (!emoteStorage.TryGetElement(shortenedPointer, out var emote))
                {
                    emote = IEmote.NewEmpty();
                    emoteStorage.Set(shortenedPointer, emote);
                }

                if (emote.Model.IsInitialized)
                    resolvedEmotes.Add(emote);
                else if (!emote.IsLoading)
                {
                    emote.UpdateLoadingStatus(true);
                    missingPointers.Add(shortenedPointer);
                }
            }
        }

        private bool CreateAssetBundlePromiseIfRequired(IEmote component, in GetEmotesByPointersIntention intention, IPartitionComponent partitionComponent)
        {
            if (!component.TryGetMainFileHash(intention.BodyShape, out string? hash))
                return false;

            if (component.AssetResults[intention.BodyShape] == null)
            {
                // The resolution of the AB promise will be finalized by FinalizeEmoteAssetBundleSystem
                var promise = AssetBundlePromise.Create(
                    World!,
                    GetAssetBundleIntention.FromHash(
                        hash! + PlatformUtils.GetCurrentPlatform(),
                        typeof(GameObject),
                        permittedSources: intention.PermittedSources,
                        customEmbeddedSubDirectory: customStreamingSubdirectory,
                        cancellationTokenSource: intention.CancellationTokenSource,
                        assetBundleManifestVersion: component.DTO.assetBundleManifestVersion,
                        parentEntityID: component.DTO.id
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
            ContentDefinition[] content = component.Model.Asset!.content;

            foreach (ContentDefinition item in content)
            {
                var audioType = item.file.ToAudioType();

                if (audioType == AudioType.UNKNOWN)
                    continue;

                urlBuilder.Clear();
                urlBuilder.AppendDomain(URLDomain.FromString(urlsSource.Url(DecentralandUrl.Content))).AppendPath(new URLPath(item.hash));
                URLAddress url = urlBuilder.Build();

                // The resolution of the audio promise will be finalized by FinalizeEmoteAssetBundleSystem
                AudioPromise promise = AudioUtils.CreateAudioClipPromise(World!, url.Value, audioType, partitionComponent);
                World!.Create(promise, component, bodyShape);
            }
        }
    }
}
