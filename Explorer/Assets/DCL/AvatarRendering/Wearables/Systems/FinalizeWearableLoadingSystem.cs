﻿using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Loading.Systems.Abstract;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using ECS;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Utility;
using AssetBundleManifestPromise = ECS.StreamableLoading.Common.AssetPromise<SceneRunner.Scene.SceneAssetBundleManifest, DCL.AvatarRendering.Wearables.Components.GetWearableAssetBundleManifestIntention>;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;
using StreamableResult = ECS.StreamableLoading.Common.Components.StreamableLoadingResult<DCL.AvatarRendering.Wearables.Components.WearablesResolution>;

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(PrepareGlobalAssetBundleLoadingParametersSystem))]
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class FinalizeWearableLoadingSystem : FinalizeElementsLoadingSystem<GetWearableDTOByPointersIntention, IWearable, WearableDTO, WearablesDTOList>
    {
        private readonly URLSubdirectory customStreamingSubdirectory;
        private readonly IRealmData realmData;
        private readonly IWearableStorage wearableStorage;

        private SingleInstanceEntity defaultWearablesState;

        public FinalizeWearableLoadingSystem(
            World world,
            IWearableStorage wearableStorage,
            IRealmData realmData,
            URLSubdirectory customStreamingSubdirectory
        ) : base(world, wearableStorage, WearableComponentsUtils.POINTERS_POOL)
        {
            this.wearableStorage = wearableStorage;
            this.realmData = realmData;
            this.customStreamingSubdirectory = customStreamingSubdirectory;
        }

        public override void Initialize()
        {
            defaultWearablesState = World!.CacheDefaultWearablesState();
        }

        protected override void Update(float t)
        {
            bool defaultWearablesResolved = defaultWearablesState.GetDefaultWearablesState(World!).ResolvedState == DefaultWearablesComponent.State.Success;

            // Only DTO loading requires realmData
            if (realmData.Configured)
                FinalizeWearableDTOQuery(World);

            ResolveWearablePromiseQuery(World, defaultWearablesResolved);

            // Asset Bundles can be Resolved with Embedded Data
            FinalizeAssetBundleManifestLoadingQuery(World, defaultWearablesResolved);
            FinalizeAssetBundleLoadingQuery(World, defaultWearablesResolved);
        }

        [Query]
        [None(typeof(StreamableResult))]
        private void ResolveWearablePromise([Data] bool defaultWearablesResolved, in Entity entity, ref GetWearablesByPointersIntention wearablesByPointersIntention, ref IPartitionComponent partitionComponent)
        {
            if (wearablesByPointersIntention.CancellationTokenSource.IsCancellationRequested)
            {
                World!.Add(entity, new StreamableResult(GetReportCategory(), new Exception("Pointer request cancelled")));
                return;
            }

            // Instead of checking particular resolution, for simplicity just check if the default wearables are resolved
            // if it is required
            if (wearablesByPointersIntention.FallbackToDefaultWearables && !defaultWearablesResolved)
                return; // Wait for default wearables to be resolved

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
                    WearableComponentsUtils.ExtractVisibleWearables(wearablesByPointersIntention.BodyShape, resolvedDTOs, resolvedDTOs.Count, ref hideWearablesResolution);

                successfulResults += wearablesByPointersIntention.Pointers.Count - hideWearablesResolution.VisibleWearables!.Count;

                for (var i = 0; i < hideWearablesResolution.VisibleWearables!.Count; i++)
                {
                    IWearable visibleWearable = hideWearablesResolution.VisibleWearables[i];

                    if (visibleWearable.IsLoading) continue;
                    if (CreateAssetBundlePromiseIfRequired(visibleWearable, wearablesByPointersIntention, partitionComponent)) continue;
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

        //TODO extract!!!
        [Query]
        private void FinalizeWearableDTO(Entity entity, ref AssetPromise<WearablesDTOList, GetWearableDTOByPointersIntention> promise, ref BodyShape bodyShape)
        {
            if (TryFinalizeIfCancelled(entity, promise))
                return;

            if (promise.SafeTryConsume(World!, GetReportCategory(), out StreamableLoadingResult<WearablesDTOList> promiseResult))
            {
                if (!promiseResult.Succeeded)
                {
                    //No wearable representation is going to be possible
                    foreach (string pointerID in promise.LoadingIntention.Pointers)
                        ReportAndFinalizeWithError(pointerID);
                }
                else
                {
                    using var _ = WearableComponentsUtils.POINTERS_POOL.Get(out var failedDTOList);
                    failedDTOList!.AddRange(promise.LoadingIntention.Pointers);

                    using (var list = promise.Result.Value.Asset.ConsumeAttachments())
                        foreach (WearableDTO assetEntity in list.Value)
                        {
                            if (wearableStorage.TryGetElementWithLogs(assetEntity, GetReportCategory(), out var component) == false)
                                continue;

                            if (component!.TryResolveDTO(new StreamableLoadingResult<WearableDTO>(assetEntity)) == false)
                                ReportHub.LogError(GetReportData(), $"Wearable DTO has already been initialized: {assetEntity.Metadata.id}");

                            failedDTOList.Remove(assetEntity.Metadata.id);
                            component.UpdateLoadingStatus(false);
                        }

                    //If this list is not empty, it means we have at least one unresolvedDTO that was not completed. We need to finalize it as error
                    foreach (var urn in failedDTOList)
                        ReportAndFinalizeWithError(urn);
                }

                promise.LoadingIntention.ReleasePointers();
                World.Destroy(entity);
            }
        }

        [Query]
        private void FinalizeAssetBundleManifestLoading([Data] bool defaultWearablesResolved, Entity entity, ref AssetBundleManifestPromise promise, ref IWearable wearable, ref BodyShape bodyShape)
        {
            if (promise.TryForgetWithEntityIfCancelled(entity, World!))
            {
                wearable.ResetManifest();
                return;
            }

            if (promise.SafeTryConsume(World, GetReportCategory(), out StreamableLoadingResult<SceneAssetBundleManifest> result))
            {
                if (result.Succeeded)
                {
                    AssetValidation.ValidateSceneAssetBundleManifest(result.Asset);
                    wearable.ManifestResult = result;
                }
                else
                    SetDefaultWearables(defaultWearablesResolved, wearable, in bodyShape);

                wearable.UpdateLoadingStatus(false);
                World.Destroy(entity);
            }
        }

        [Query]
        private void FinalizeAssetBundleLoading(
            [Data] bool defaultWearablesResolved,
            Entity entity,
            ref AssetBundlePromise promise,
            ref IWearable wearable,
            in BodyShape bodyShape,
            int index
        )
        {
            if (promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested)
            {
                ResetWearableResultOnCancellation(wearable, in bodyShape, index);
                promise.ForgetLoading(World);
                World.Destroy(entity);
                return;
            }

            if (promise.SafeTryConsume(World, GetReportCategory(), out StreamableLoadingResult<AssetBundleData> result))
            {
                // every asset in the batch is mandatory => if at least one has already failed set the default wearables
                if (result.Succeeded && !AnyAssetHasFailed(wearable, bodyShape))
                    SetWearableResult(wearable, result, in bodyShape, index);
                else
                    SetDefaultWearables(defaultWearablesResolved, wearable, in bodyShape);

                wearable.UpdateLoadingStatus(!AllAssetsAreLoaded(wearable, bodyShape));
                World.Destroy(entity);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool AllAssetsAreLoaded(IWearable wearable, BodyShape bodyShape)
        {
            for (var i = 0; i < wearable.WearableAssetResults[bodyShape].Results.Length; i++)
                if (wearable.WearableAssetResults[bodyShape].Results[i] is not { IsInitialized: true })
                    return false;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool AnyAssetHasFailed(IWearable wearable, BodyShape bodyShape) =>
            wearable.WearableAssetResults[bodyShape].ReplacedWithDefaults;

        private bool CreateAssetBundlePromiseIfRequired(IWearable component, in GetWearablesByPointersIntention intention, IPartitionComponent partitionComponent)
        {
            // Do not repeat the promise if already failed once. Otherwise it will end up in an endless loading:true state
            if (component.ManifestResult is { Succeeded: false }) return false;

            // Manifest is required for Web loading only
            if (component.ManifestResult == null && EnumUtils.HasFlag(intention.PermittedSources, AssetSource.WEB))
                return component.CreateAssetBundleManifestPromise(World, intention.BodyShape, intention.CancellationTokenSource, partitionComponent);

            if (component.TryCreateAssetBundlePromise(in intention, customStreamingSubdirectory, partitionComponent, World, GetReportCategory()))
            {
                component.UpdateLoadingStatus(true);
                return true;
            }

            return false;
        }

        private void CreateMissingPointersPromise(List<URN> missingPointers, GetWearablesByPointersIntention intention, IPartitionComponent partitionComponent)
        {
            var wearableDtoByPointersIntention = new GetWearableDTOByPointersIntention(
                missingPointers,
                new CommonLoadingArguments(realmData.Ipfs.EntitiesActiveEndpoint, cancellationTokenSource: intention.CancellationTokenSource));

            var promise = AssetPromise<WearablesDTOList, GetWearableDTOByPointersIntention>.Create(World, wearableDtoByPointersIntention, partitionComponent);

            World.Create(promise, intention.BodyShape, partitionComponent);
        }

        private void SetDefaultWearables(bool defaultWearablesLoaded, IWearable wearable, in BodyShape bodyShape)
        {
            if (!defaultWearablesLoaded)
            {
                StreamableLoadingResult<AttachmentAssetBase> failedResult = new StreamableLoadingResult<AssetBundleData>(
                    GetReportData(),
                    new Exception($"Default wearable {wearable.DTO.GetHash()} failed to load")
                ).ToWearableAsset(wearable);

                if (wearable.IsUnisex() && wearable.HasSameModelsForAllGenders())
                {
                    SetFailure(BodyShape.MALE);
                    SetFailure(BodyShape.FEMALE);
                }
                else
                    SetFailure(bodyShape);

                return;

                void SetFailure(BodyShape bs)
                {
                    // the destination array might be not created if DTO itself has failed to load
                    ref var result = ref wearable.WearableAssetResults[bs];
                    result.Results ??= new StreamableLoadingResult<AttachmentAssetBase>?[1]; // default capacity, can't tell without the DTO
                    result.ReplacedWithDefaults = true;
                    result.Results[0] = failedResult;
                }
            }

            ReportHub.Log(GetReportData(), $"Request for wearable with hash {wearable.DTO.GetHash()} and urn {wearable.GetUrn()} failed, loading default wearable");

            if (wearable.IsUnisex() && wearable.HasSameModelsForAllGenders())
            {
                CopyDefaultResults(BodyShape.MALE);
                CopyDefaultResults(BodyShape.FEMALE);
            }
            else
                CopyDefaultResults(bodyShape);

            return;

            void CopyDefaultResults(BodyShape bs)
            {
                IWearable defaultWearable = wearableStorage.GetDefaultWearable(bs, wearable.GetCategory());
                var defaultWearableResults = defaultWearable.WearableAssetResults[bs];

                // the destination array might be not created if DTO itself has failed to load
                ref var result = ref wearable.WearableAssetResults[bs];
                result.Results ??= new StreamableLoadingResult<AttachmentAssetBase>?[defaultWearableResults.Results.Length];
                result.ReplacedWithDefaults = true;

                Array.Copy(defaultWearableResults.Results, result.Results, defaultWearableResults.Results.Length);
            }
        }

        /// <summary>
        ///     If the loading of the asset was cancelled reset the promise so we can start downloading it again with a new intent
        /// </summary>
        private static void ResetWearableResultOnCancellation(IWearable wearable, in BodyShape bodyShape, int index)
        {
            wearable.UpdateLoadingStatus(false);

            void ResetBodyShape(BodyShape bs)
            {
                ref WearableAssets assets = ref wearable.WearableAssetResults[bs];
                if (assets.Results == null) return;

                if (assets.Results[index] is { IsInitialized: false })
                    assets.Results[index] = null;
            }

            if (wearable.IsUnisex() && wearable.HasSameModelsForAllGenders())
            {
                ResetBodyShape(BodyShape.MALE);
                ResetBodyShape(BodyShape.FEMALE);
            }
            else
                ResetBodyShape(bodyShape);
        }

        private static void SetWearableResult(IWearable wearable, StreamableLoadingResult<AssetBundleData> result, in BodyShape bodyShape, int index)
        {
            StreamableLoadingResult<AttachmentAssetBase> wearableResult = result.ToWearableAsset(wearable);

            if (wearable.IsUnisex() && wearable.HasSameModelsForAllGenders())
            {
                SetByRef(BodyShape.MALE);
                SetByRef(BodyShape.FEMALE);
            }
            else
                SetByRef(bodyShape);

            return;

            void SetByRef(BodyShape bodyShape)
            {
                ref var asset = ref wearable.WearableAssetResults[bodyShape];
                asset.Results[index] = wearableResult;
            }
        }
    }
}
