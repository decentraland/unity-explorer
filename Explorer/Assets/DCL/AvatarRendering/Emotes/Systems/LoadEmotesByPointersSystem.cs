using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using AssetManagement;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using DCL.SDKComponents.AudioSources;
using DCL.WebRequests;
using ECS;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using StreamableResult = ECS.StreamableLoading.Common.Components.StreamableLoadingResult<DCL.AvatarRendering.Emotes.EmotesResolution>;
using EmotesFromRealmPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesDTOList,
    DCL.AvatarRendering.Emotes.GetEmotesByPointersFromRealmIntention>;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;
using AudioPromise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.AudioClip, ECS.StreamableLoading.AudioClips.GetAudioClipIntention>;

namespace DCL.AvatarRendering.Emotes
{
    /// <summary>
    ///     TODO this system should be generalized with <see cref="ResolveWearableByPointerSystem" />
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.EMOTE)]
    public partial class LoadEmotesByPointersSystem : LoadSystemBase<EmotesDTOList, GetEmotesByPointersFromRealmIntention>
    {
        // When the number of wearables to request is greater than MAX_WEARABLES_PER_REQUEST, we split the request into several smaller ones.
        // In this way we avoid to send a very long url string that would fail due to the web request size limitations.
        private const int MAX_WEARABLES_PER_REQUEST = 200;

        private static readonly ThreadSafeListPool<EmoteDTO> DTO_POOL = new (MAX_WEARABLES_PER_REQUEST, 50);

        private readonly StringBuilder bodyBuilder = new ();
        private readonly URLSubdirectory customStreamingSubdirectory;
        private readonly IWebRequestController webRequestController;
        private readonly IEmoteCache emoteCache;
        private readonly IRealmData realmData;
        private readonly URLBuilder urlBuilder;

        public LoadEmotesByPointersSystem(World world,
            IWebRequestController webRequestController,
            IStreamableCache<EmotesDTOList, GetEmotesByPointersFromRealmIntention> cache,
            IEmoteCache emoteCache,
            IRealmData realmData,
            URLSubdirectory customStreamingSubdirectory)
            : base(world, cache)
        {
            this.webRequestController = webRequestController;
            this.emoteCache = emoteCache;
            this.realmData = realmData;
            this.customStreamingSubdirectory = customStreamingSubdirectory;
            urlBuilder = new URLBuilder();
        }

        protected override void Update(float t)
        {
            base.Update(t);

            GetEmotesFromRealmQuery(World, t);
            FinalizeEmoteDTOQuery(World);
        }

        protected override async UniTask<StreamableLoadingResult<EmotesDTOList>> FlowInternalAsync(
            GetEmotesByPointersFromRealmIntention intention, IAcquiredBudget acquiredBudget,
            IPartitionComponent partition, CancellationToken ct)
        {
            var finalTargetList = new List<EmoteDTO>();

            int numberOfPartialRequests = (intention.Pointers.Count + MAX_WEARABLES_PER_REQUEST - 1) / MAX_WEARABLES_PER_REQUEST;

            var pointer = 0;

            for (var i = 0; i < numberOfPartialRequests; i++)
            {
                int numberOfWearablesToRequest = Mathf.Min(intention.Pointers.Count - pointer, MAX_WEARABLES_PER_REQUEST);

                await DoPartialRequestAsync(intention.CommonArguments.URL, intention.Pointers,
                    pointer, pointer + numberOfWearablesToRequest, finalTargetList, ct);

                pointer += numberOfWearablesToRequest;
            }

            return new StreamableLoadingResult<EmotesDTOList>(new EmotesDTOList(finalTargetList));
        }

        private async UniTask DoPartialRequestAsync(URLAddress url,
            IReadOnlyList<URN> wearablesToRequest, int startIndex, int endIndex, List<EmoteDTO> results, CancellationToken ct)
        {
            await UniTask.SwitchToMainThread();

            bodyBuilder.Clear();
            bodyBuilder.Append("{\"pointers\":[");

            for (int i = startIndex; i < endIndex; ++i)
            {
                // String Builder has overloads for int to prevent allocations
                bodyBuilder.Append('\"');
                bodyBuilder.Append(wearablesToRequest[i]);
                bodyBuilder.Append('\"');

                if (i != wearablesToRequest.Count - 1)
                    bodyBuilder.Append(",");
            }

            bodyBuilder.Append("]}");

            using PoolExtensions.Scope<List<EmoteDTO>> dtoPooledList = DTO_POOL.AutoScope();

            await webRequestController.PostAsync(new CommonArguments(url), GenericPostArguments.CreateJson(bodyBuilder.ToString()), ct)
                                      .OverwriteFromJsonAsync(dtoPooledList.Value, WRJsonParser.Newtonsoft, WRThreadFlags.SwitchToThreadPool);

            lock (results) { results.AddRange(dtoPooledList.Value); }
        }

        [Query]
        [None(typeof(StreamableResult))]
        private void GetEmotesFromRealm([Data] float dt, in Entity entity,
            ref GetEmotesByPointersIntention intention,
            ref IPartitionComponent partitionComponent)
        {
            if (intention.CancellationTokenSource.IsCancellationRequested)
            {
                if (!World.Has<StreamableResult>(entity))
                    World.Add(entity, new StreamableResult(new OperationCanceledException("Pointer request cancelled")));

                return;
            }

            intention.ElapsedTime += dt;

            bool isTimeout = intention.ElapsedTime >= intention.Timeout;

            if (isTimeout)
            {
                if (!World.Has<StreamableResult>(entity))
                {
                    var pointersStrLog = string.Join(",", intention.Pointers);
                    ReportHub.LogWarning(GetReportCategory(), $"Loading emotes timed out, {pointersStrLog}");
                    World.Add(entity, new StreamableResult(new TimeoutException($"Emote intention timeout {pointersStrLog}")));
                }

                return;
            }

            List<URN> missingPointersTmp = ListPool<URN>.Get();
            List<IEmote> resolvedEmotesTmp = ListPool<IEmote>.Get();

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

                URN shortenedPointer = loadingIntentionPointer;
                shortenedPointer = shortenedPointer.Shorten();

                if (!emoteCache.TryGetEmote(shortenedPointer, out IEmote emote))
                {
                    if (!intention.ProcessedPointers.Contains(loadingIntentionPointer))
                    {
                        missingPointersTmp.Add(shortenedPointer);
                        intention.ProcessedPointers.Add(loadingIntentionPointer);
                    }

                    continue;
                }

                if (emote.Model.Succeeded)
                    resolvedEmotesTmp.Add(emote);
            }

            if (missingPointersTmp.Count > 0)
            {
                var promise = EmotesFromRealmPromise.Create(World, new GetEmotesByPointersFromRealmIntention(missingPointersTmp.ToList(),
                        new CommonLoadingArguments(realmData.Ipfs.EntitiesActiveEndpoint)),
                    partitionComponent);

                World.Create(promise, intention.BodyShape, partitionComponent);

                ListPool<URN>.Release(missingPointersTmp);
                ListPool<IEmote>.Release(resolvedEmotesTmp);

                return;
            }

            var emotesWithResponse = 0;

            foreach (IEmote emote in resolvedEmotesTmp)
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
                {
                    // Reference must be added only once when the wearable is resolved
                    if (!intention.SuccessfulPointers.Contains(emote.GetUrn()))
                    {
                        intention.SuccessfulPointers.Add(emote.GetUrn());

                        // We need to add a reference here, so it is not lost if the flow interrupts in between (i.e. before creating instances of CachedWearable)
                        emote.AssetResults[intention.BodyShape]?.Asset.AddReference();
                    }
                }
            }

            bool isSucceeded = emotesWithResponse == intention.Pointers.Count;

            if (isSucceeded)
                World.Add(entity, new StreamableResult(new EmotesResolution(resolvedEmotesTmp.ToList(), intention.Pointers.Count)));

            ListPool<URN>.Release(missingPointersTmp);
            ListPool<IEmote>.Release(resolvedEmotesTmp);
        }

        [Query]
        private void FinalizeEmoteDTO(in Entity entity,
            ref AssetPromise<EmotesDTOList, GetEmotesByPointersFromRealmIntention> promise)
        {
            if (promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested)
            {
                promise.ForgetLoading(World);
                World.Destroy(entity);
                return;
            }

            if (promise.SafeTryConsume(World, out StreamableLoadingResult<EmotesDTOList> promiseResult))
            {
                if (!promiseResult.Succeeded)
                {
                    foreach (string pointerID in promise.LoadingIntention.Pointers)
                        if (emoteCache.TryGetEmote(pointerID, out IEmote component))
                            component.IsLoading = false;
                }
                else
                {
                    foreach (EmoteDTO assetEntity in promiseResult.Asset.Value)
                    {
                        IEmote component = emoteCache.GetOrAddEmoteByDTO(assetEntity);
                        component.Model = new StreamableLoadingResult<EmoteDTO>(assetEntity);
                        component.IsLoading = false;
                    }
                }

                World.Destroy(entity);
            }
        }

        private bool CreateAssetBundlePromiseIfRequired(IEmote component, in GetEmotesByPointersIntention intention, IPartitionComponent partitionComponent)
        {
            // Manifest is required for Web loading only
            if (component.ManifestResult == null
                && EnumUtils.HasFlag(intention.PermittedSources, AssetSource.WEB)

                // Skip processing manifest for embedded emotes which do not start with 'urn'
                && component.GetUrn().IsValid())
            {
                // The resolution of the AB promise will be finalized by FinalizeEmoteAssetBundleSystem
                return component.CreateAssetBundleManifestPromise(World, intention.BodyShape, intention.CancellationTokenSource, partitionComponent);
            }

            if (!component.TryGetMainFileHash(intention.BodyShape, out string? hash))
                return false;

            if (component.AssetResults[intention.BodyShape] == null)
            {
                SceneAssetBundleManifest? manifest = !EnumUtils.HasFlag(intention.PermittedSources, AssetSource.WEB) ? null : component.ManifestResult?.Asset;

                // The resolution of the AB promise will be finalized by FinalizeEmoteAssetBundleSystem
                var promise = AssetBundlePromise.Create(World,
                    GetAssetBundleIntention.FromHash(typeof(GameObject),
                        hash! + PlatformUtils.GetPlatform(),
                        permittedSources: intention.PermittedSources,
                        customEmbeddedSubDirectory: customStreamingSubdirectory,
                        manifest: manifest, cancellationTokenSource: intention.CancellationTokenSource),
                    partitionComponent);

                TryCreateAudioClipPromise(component, intention.BodyShape, partitionComponent);

                component.IsLoading = true;
                World.Create(promise, component, intention.BodyShape);
                return true;
            }

            return false;
        }

        private void TryCreateAudioClipPromise(IEmote component, BodyShape bodyShape, IPartitionComponent partitionComponent)
        {
            AvatarAttachmentDTO.Content[]? content = component.Model.Asset!.content;

            foreach (AvatarAttachmentDTO.Content item in content)
            {
                var audioType = item.file.ToAudioType();

                if (audioType == AudioType.UNKNOWN)
                    continue;

                urlBuilder.Clear();
                urlBuilder.AppendDomain(realmData.Ipfs.ContentBaseUrl).AppendPath(new URLPath(item.hash));
                URLAddress url = urlBuilder.Build();

                // The resolution of the audio promise will be finalized by FinalizeEmoteAssetBundleSystem
                AudioPromise promise = AudioUtils.CreateAudioClipPromise(World, url.Value, audioType, partitionComponent);
                World.Create(promise, component, bodyShape);
            }
        }
    }
}