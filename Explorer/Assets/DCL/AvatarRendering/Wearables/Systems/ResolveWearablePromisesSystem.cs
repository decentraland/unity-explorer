using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using DCL.Analytics.Systems;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Optimization.PerformanceBudgeting;
using ECS;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

using StreamableResult = ECS.StreamableLoading.Common.Components.StreamableLoadingResult<DCL.AvatarRendering.Wearables.Components.WearablesResolution>;

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(FinalizeAssetBundleWearableLoadingSystem))]
    [UpdateAfter(typeof(FinalizeRawWearableLoadingSystem))]
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class ResolveWearablePromisesSystem : BaseUnityLoopSystem
    {
        private readonly URLSubdirectory customStreamingSubdirectory;
        private readonly IWearableStorage wearableStorage;
        private readonly IDecentralandUrlsSource urlsSource;
        private readonly ConcurrentLoadingPerformanceBudget loadingBudget;
        private readonly int estimatedAssetsPerAvatar;

        // Per-frame aggregation only
        private readonly List<(Entity entity, float sqrDistance)> assetPhaseCandidates = new ();

        public ResolveWearablePromisesSystem(
            World world,
            IWearableStorage wearableStorage,
            IDecentralandUrlsSource urlsSource,
            URLSubdirectory customStreamingSubdirectory,
            ConcurrentLoadingPerformanceBudget loadingBudget,
            int estimatedAssetsPerAvatar
            ) : base(world)
        {
            this.wearableStorage = wearableStorage;
            this.urlsSource = urlsSource;
            this.customStreamingSubdirectory = customStreamingSubdirectory;
            this.loadingBudget = loadingBudget;
            this.estimatedAssetsPerAvatar = Math.Max(1, estimatedAssetsPerAvatar);
        }

        public override void Initialize()
        {
        }

        protected override void Update(float t)
        {
            assetPhaseCandidates.Clear();
            ResolveWearablePromiseQuery(World);
            AdmitNextAvatarsToAssetPhase();
        }

        /// <summary>
        ///     Admits DTO-ready avatars into the asset phase, nearest first, only while the shared download budget has
        ///     free slots. Each admission is assumed to add <see cref="estimatedAssetsPerAvatar" /> downloads to the
        ///     pipe, so we stop once we predict it is full instead of flooding it. This keeps the budget saturated (the
        ///     admitted avatars' downloads fill it) while still serializing at the avatar granularity, so avatars
        ///     complete and reveal one wave after another. As avatars finish they release budget and the next wave is
        ///     admitted, so admission self-tunes to the completion rate and a stalled avatar (holding no budget) never
        ///     blocks the queue.
        /// </summary>
        private void AdmitNextAvatarsToAssetPhase()
        {
            if (assetPhaseCandidates.Count == 0)
                return;

            // CurrentBudget is the whole app's free concurrent-download slots; admitted avatars compete for the same
            // pool as scenes and everything else, which is exactly what we want to avoid oversubscribing.
            int freeSlots = loadingBudget.CurrentBudget;

            if (freeSlots <= 0)
                return;

            // Nearest first, so the stagger reveals the most visible avatars earlier
            assetPhaseCandidates.Sort(static (c1, c2) => c1.sqrDistance.CompareTo(c2.sqrDistance));

            // Admit at least the nearest candidate whenever there is any room (freeSlots checked before the decrement),
            // so avatars keep progressing even when the pipe only has a sliver free.
            for (var i = 0; i < assetPhaseCandidates.Count && freeSlots > 0; i++)
            {
                World.Add(assetPhaseCandidates[i].entity, new AvatarWearableAssetsInFlight());
                freeSlots -= estimatedAssetsPerAvatar;
            }
        }

        [Query]
        [None(typeof(StreamableResult))]
        private void ResolveWearablePromise(Entity entity, ref GetWearablesByPointersIntention wearablesByPointersIntention, ref IPartitionComponent partitionComponent)
        {
            if (wearablesByPointersIntention.CancellationTokenSource.IsCancellationRequested)
            {
                World!.Add(entity, new StreamableResult(GetReportCategory(), new OperationCanceledException("Pointer request cancelled")));
                return;
            }

            List<URN> missingPointers = WearableComponentsUtils.POINTERS_POOL.Get()!;
            List<IWearable> resolvedDTOs = WearableComponentsUtils.WEARABLES_POOL.Get()!;

            var resolvedResults = 0;
            int finishedDTOs = 0;

            for (var index = 0; index < wearablesByPointersIntention.Pointers.Count; index++)
            {
                URN loadingIntentionPointer = wearablesByPointersIntention.Pointers[index];

                if (loadingIntentionPointer.IsNullOrEmpty())
                {
                    ReportHub.LogError(
                        GetReportData(),
                        $"ResolveWearableByPointerSystem: Null pointer found in the list of pointers: index {index}"
                    );

                    continue;
                }

                URN shortenedPointer = loadingIntentionPointer;
                loadingIntentionPointer = shortenedPointer.Shorten();

                if (!wearableStorage.TryGetElement(loadingIntentionPointer, out var wearable))
                {
                    wearable = IWearable.NewEmpty();
                    wearableStorage.Set(loadingIntentionPointer, wearable);
                }

                if (wearable.Model.Succeeded)
                {
                    finishedDTOs++;
                    resolvedDTOs.Add(wearable);
                }
                else if (wearable.Model.Exception != null)
                    finishedDTOs++;
                else if (!wearable.IsLoading)
                {
                    wearable.UpdateLoadingStatus(true);
                    missingPointers.Add(loadingIntentionPointer);
                }
            }

            if (missingPointers.Count > 0)
            {
                CreateMissingPointersPromise(missingPointers, ref wearablesByPointersIntention, partitionComponent);
                return;
            }

            // Avatars enter the asset phase through a nearest-first gate (see AdmitNextAvatarsToAssetPhase) so their
            // downloads keep the shared pipe busy without flooding it, and they complete one wave after another instead
            // of interleaving and all finishing together at the tail of one queue. Until admitted, an avatar with all
            // its DTOs resolved is only a candidate.
            // DTO resolution above is exempt: definitions are batched into a single request and shared via the storage.
            if (finishedDTOs == wearablesByPointersIntention.Pointers.Count && !World.Has<AvatarWearableAssetsInFlight>(entity))
            {
                assetPhaseCandidates.Add((entity, partitionComponent.RawSqrDistance));

                WearableComponentsUtils.WEARABLES_POOL.Release(resolvedDTOs);
                WearableComponentsUtils.POINTERS_POOL.Release(missingPointers);
                return;
            }

            ref HideWearablesResolution hideWearablesResolution = ref wearablesByPointersIntention.HideWearablesResolution;

            if (finishedDTOs == wearablesByPointersIntention.Pointers.Count)
            {
                if (hideWearablesResolution.VisibleWearables == null)
                    WearableComponentsUtils.ExtractVisibleWearables(wearablesByPointersIntention.BodyShape, resolvedDTOs, ref hideWearablesResolution);

                resolvedResults += wearablesByPointersIntention.Pointers.Count - hideWearablesResolution.VisibleWearables!.Count;

                for (var i = 0; i < hideWearablesResolution.VisibleWearables!.Count; i++)
                {
                    IWearable visibleWearable = hideWearablesResolution.VisibleWearables[i];

                    if (visibleWearable.IsLoading) continue;
                    if (CreateAssetPromiseIfRequired(visibleWearable, wearablesByPointersIntention, partitionComponent)) continue;
                    if (!visibleWearable.HasEssentialAssetsResolved(wearablesByPointersIntention.BodyShape)) continue;

                    resolvedResults++;

                    // Reference must be added only once when the wearable is resolved
                    if (BitWiseUtils.TrySetBit(ref wearablesByPointersIntention.ResolvedWearablesIndices, i))

                        // We need to add a reference here, so it is not lost if the flow interrupts in between (i.e. before creating instances of CachedWearable)
                        visibleWearable.WearableAssetResults[wearablesByPointersIntention.BodyShape].AddReference();
                }
            }

            WearableComponentsUtils.WEARABLES_POOL.Release(resolvedDTOs);

            // If there are no missing pointers, we release the list
            WearableComponentsUtils.POINTERS_POOL.Release(missingPointers);

            if (resolvedResults == wearablesByPointersIntention.Pointers.Count)
            {
                //One last safeguard in case the dto was successfull but the assets failed
                WearableComponentsUtils.ConfirmWearableVisibility(wearablesByPointersIntention.BodyShape, ref hideWearablesResolution);
                World.Add(entity, new StreamableResult(new WearablesResolution(hideWearablesResolution.VisibleWearables!, hideWearablesResolution.HiddenCategories!)));
            }
        }

        private void CreateMissingPointersPromise(List<URN> missingPointers, ref GetWearablesByPointersIntention intention, IPartitionComponent partitionComponent)
        {
            var wearableDtoByPointersIntention = new GetWearableDTOByPointersIntention(
                missingPointers,
                new CommonLoadingArguments(urlsSource.Url(DecentralandUrl.EntitiesActiveElements), cancellationTokenSource: CancellationTokenSource.CreateLinkedTokenSource(intention.CancellationTokenSource.Token)));

            var promise = AssetPromise<WearablesDTOList, GetWearableDTOByPointersIntention>.Create(World, wearableDtoByPointersIntention, partitionComponent);

            intention.MissingPointersCount = missingPointers.Count;

            World.Create(promise, intention.BodyShape, partitionComponent);
        }

        private bool CreateAssetPromiseIfRequired(IWearable component, in GetWearablesByPointersIntention intention, IPartitionComponent partitionComponent)
        {
            bool dtoHasContentDownloadUrl = !string.IsNullOrEmpty(component.DTO.ContentDownloadUrl);

            if (!dtoHasContentDownloadUrl)
            {
                AssetBundleManifestVersion? assetBundleManifestVersion = component.DTO.assetBundleManifestVersion;

                // If the manifest version is null, we bail out and wait till it's loaded (this method will be called again)
                if (assetBundleManifestVersion == null) return true;

                // Do not repeat the promise if already failed once. Otherwise it will end up in an endless loading:true state
                if (assetBundleManifestVersion.assetBundleManifestRequestFailed) return false;
            }

            if (component.TryCreateAssetPromise(in intention, customStreamingSubdirectory, partitionComponent, World, GetReportCategory()))
            {
                component.UpdateLoadingStatus(true);
                return true;
            }

            return false;
        }
    }
}
