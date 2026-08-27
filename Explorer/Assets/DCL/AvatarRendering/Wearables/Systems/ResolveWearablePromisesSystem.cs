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
        /// <summary>
        ///     A resolution stuck in the asset phase for this long stops holding an in-flight slot, so a wearable
        ///     whose assets never resolve cannot starve the avatars queued behind it.
        /// </summary>
        private const float IN_FLIGHT_STUCK_TIMEOUT_SECS = 30f;

        private readonly URLSubdirectory customStreamingSubdirectory;
        private readonly IWearableStorage wearableStorage;
        private readonly IDecentralandUrlsSource urlsSource;
        private readonly int maxAvatarsWithAssetsInFlight;

        // Per-frame aggregation only
        private readonly List<(Entity entity, float sqrDistance)> assetPhaseCandidates = new ();
        private int avatarsWithAssetsInFlight;

        public ResolveWearablePromisesSystem(
            World world,
            IWearableStorage wearableStorage,
            IDecentralandUrlsSource urlsSource,
            URLSubdirectory customStreamingSubdirectory,
            int maxAvatarsWithAssetsInFlight
            ) : base(world)
        {
            this.wearableStorage = wearableStorage;
            this.urlsSource = urlsSource;
            this.customStreamingSubdirectory = customStreamingSubdirectory;
            this.maxAvatarsWithAssetsInFlight = Math.Max(1, maxAvatarsWithAssetsInFlight);
        }

        public override void Initialize()
        {
        }

        protected override void Update(float t)
        {
            assetPhaseCandidates.Clear();
            ResolveWearablePromiseQuery(World);

            avatarsWithAssetsInFlight = 0;
            CountAvatarsWithAssetsInFlightQuery(World, t);
            AdmitNextAvatarsToAssetPhase();
        }

        [Query]
        [None(typeof(StreamableResult))]
        private void CountAvatarsWithAssetsInFlight([Data] float dt, ref AvatarWearableAssetsInFlight inFlight)
        {
            inFlight.Age += dt;

            if (inFlight.Age < IN_FLIGHT_STUCK_TIMEOUT_SECS)
                avatarsWithAssetsInFlight++;
        }

        private void AdmitNextAvatarsToAssetPhase()
        {
            int freeSlots = maxAvatarsWithAssetsInFlight - avatarsWithAssetsInFlight;

            if (freeSlots <= 0 || assetPhaseCandidates.Count == 0)
                return;

            // Nearest first, so the stagger reveals the most visible avatars earlier
            assetPhaseCandidates.Sort(static (c1, c2) => c1.sqrDistance.CompareTo(c2.sqrDistance));

            for (var i = 0; i < assetPhaseCandidates.Count && i < freeSlots; i++)
                World.Add(assetPhaseCandidates[i].entity, new AvatarWearableAssetsInFlight());
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

            // Only a capped number of avatars may load wearable assets at once, so they complete one after another
            // instead of interleaving downloads and all finishing together at the tail of one shared queue.
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
