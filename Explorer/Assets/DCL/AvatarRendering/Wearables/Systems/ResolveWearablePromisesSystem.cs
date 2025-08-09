using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using ECS;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
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
        private readonly IRealmData realmData;

        public ResolveWearablePromisesSystem(
            World world,
            IWearableStorage wearableStorage,
            IRealmData realmData,
            URLSubdirectory customStreamingSubdirectory
            ) : base(world)
        {
            this.wearableStorage = wearableStorage;
            this.realmData = realmData;
            this.customStreamingSubdirectory = customStreamingSubdirectory;
        }

        public override void Initialize()
        {
        }

        protected override void Update(float t)
        {
            ResolveWearablePromiseQuery(World);
        }

        [Query]
        [None(typeof(StreamableResult))]
        private void ResolveWearablePromise(in Entity entity, ref GetWearablesByPointersIntention wearablesByPointersIntention, ref IPartitionComponent partitionComponent)
        {
            if (wearablesByPointersIntention.CancellationTokenSource.IsCancellationRequested)
            {
                World!.Add(entity, new StreamableResult(GetReportCategory(), new OperationCanceledException("Pointer request cancelled")));
                return;
            }

            List<URN> missingPointers = WearableComponentsUtils.POINTERS_POOL.Get()!;
            List<IWearable> resolvedDTOs = WearableComponentsUtils.WEARABLES_POOL.Get()!;

            var successfulResults = 0;
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
                CreateMissingPointersPromise(missingPointers, wearablesByPointersIntention, partitionComponent);
                return;
            }

            ref HideWearablesResolution hideWearablesResolution = ref wearablesByPointersIntention.HideWearablesResolution;

            if (finishedDTOs == wearablesByPointersIntention.Pointers.Count)
            {
                if (hideWearablesResolution.VisibleWearables == null)
                    WearableComponentsUtils.ExtractVisibleWearables(wearablesByPointersIntention.BodyShape, resolvedDTOs, ref hideWearablesResolution);

                successfulResults += wearablesByPointersIntention.Pointers.Count - hideWearablesResolution.VisibleWearables!.Count;

                for (var i = 0; i < hideWearablesResolution.VisibleWearables!.Count; i++)
                {
                    IWearable visibleWearable = hideWearablesResolution.VisibleWearables[i];

                    if (visibleWearable.IsLoading) continue;
                    if (CreateAssetPromiseIfRequired(visibleWearable, wearablesByPointersIntention, partitionComponent)) continue;
                    if (!visibleWearable.HasEssentialAssetsResolved(wearablesByPointersIntention.BodyShape)) continue;

                    successfulResults++;

                    // Reference must be added only once when the wearable is resolved
                    if (BitWiseUtils.TrySetBit(ref wearablesByPointersIntention.ResolvedWearablesIndices, i))

                        // We need to add a reference here, so it is not lost if the flow interrupts in between (i.e. before creating instances of CachedWearable)
                        visibleWearable.WearableAssetResults[wearablesByPointersIntention.BodyShape].AddReference();
                }
            }

            WearableComponentsUtils.WEARABLES_POOL.Release(resolvedDTOs);

            // If there are no missing pointers, we release the list
            WearableComponentsUtils.POINTERS_POOL.Release(missingPointers);

            if (successfulResults == wearablesByPointersIntention.Pointers.Count)
                World.Add(entity, new StreamableResult(new WearablesResolution(hideWearablesResolution.VisibleWearables, hideWearablesResolution.HiddenCategories)));
        }

        private void CreateMissingPointersPromise(List<URN> missingPointers, GetWearablesByPointersIntention intention, IPartitionComponent partitionComponent)
        {
            var wearableDtoByPointersIntention = new GetWearableDTOByPointersIntention(
                missingPointers,
                new CommonLoadingArguments(realmData.Ipfs.EntitiesActiveEndpoint, cancellationTokenSource: intention.CancellationTokenSource));

            var promise = AssetPromise<WearablesDTOList, GetWearableDTOByPointersIntention>.Create(World, wearableDtoByPointersIntention, partitionComponent);

            World.Create(promise, intention.BodyShape, partitionComponent);
        }

        private bool CreateAssetPromiseIfRequired(IWearable component, in GetWearablesByPointersIntention intention, IPartitionComponent partitionComponent)
        {
            bool dtoHasContentDownloadUrl = !string.IsNullOrEmpty(component.DTO.ContentDownloadUrl);

            // Do not repeat the promise if already failed once. Otherwise it will end up in an endless loading:true state
            if (!dtoHasContentDownloadUrl && component.DTO.assetBundleManifestRequestFailed) return false;

            if (EnumUtils.HasFlag(intention.PermittedSources, AssetSource.WEB) // Manifest is required for Web loading only
                && !dtoHasContentDownloadUrl && string.IsNullOrEmpty(component.DTO.assetBundleManifestVersion))
                return component.CreateAssetBundleManifestPromise(World, intention.BodyShape, intention.CancellationTokenSource, partitionComponent);

            if (component.TryCreateAssetPromise(in intention, customStreamingSubdirectory, partitionComponent, World, GetReportCategory()))
            {
                component.UpdateLoadingStatus(true);
                return true;
            }

            return false;
        }
    }
}
