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
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Cysharp.Threading.Tasks;
using Utility;
using AssetBundleManifestPromise = ECS.StreamableLoading.Common.AssetPromise<SceneRunner.Scene.SceneAssetBundleManifest, DCL.AvatarRendering.Wearables.Components.GetWearableAssetBundleManifestIntention>;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;
using StreamableResult = ECS.StreamableLoading.Common.Components.StreamableLoadingResult<DCL.AvatarRendering.Wearables.Components.WearablesResolution>;

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(PrepareGlobalAssetBundleLoadingParametersSystem))]
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class ResolveWearableByPointerSystem : BaseUnityLoopSystem
    {
        private readonly URLSubdirectory customStreamingSubdirectory;
        private readonly IRealmData realmData;
        private readonly URLDomain assetBundleURL;
        private readonly IWearableCatalog wearableCatalog;

        private SingleInstanceEntity defaultWearablesState;

        public ResolveWearableByPointerSystem(World world, IWearableCatalog wearableCatalog, IRealmData realmData,
            URLSubdirectory customStreamingSubdirectory, URLDomain assetBundleURL) : base(world)
        {
            this.wearableCatalog = wearableCatalog;
            this.realmData = realmData;
            this.customStreamingSubdirectory = customStreamingSubdirectory;
            this.assetBundleURL = assetBundleURL;
        }

        public override void Initialize()
        {
            defaultWearablesState = World.CacheDefaultWearablesState();
        }

        protected override void Update(float t)
        {
            bool defaultWearablesResolved = defaultWearablesState.GetDefaultWearablesState(World).ResolvedState == DefaultWearablesComponent.State.Success;

            // Only DTO loading requires realmData
            if (realmData.Configured)
                FinalizeWearableDTOQuery(World, defaultWearablesResolved);

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
                World.Add(entity, new StreamableResult(new Exception("Pointer request cancelled")));
                return;
            }

            // Instead of checking particular resolution, for simplicity just check if the default wearables are resolved
            // if it is required
            if (wearablesByPointersIntention.FallbackToDefaultWearables && !defaultWearablesResolved)
                return; // Wait for default wearables to be resolved

            List<URN> missingPointers = WearableComponentsUtils.POINTERS_POOL.Get();
            List<IWearable> resolvedDTOs = WearableComponentsUtils.WEARABLES_POOL.Get();

            var successfulResults = 0;
            var successfulDtos = 0;

            for (var index = 0; index < wearablesByPointersIntention.Pointers.Count; index++)
            {
                URN loadingIntentionPointer = wearablesByPointersIntention.Pointers[index];

                if (loadingIntentionPointer.IsNullOrEmpty())
                {
                    ReportHub.LogError(
                        GetReportCategory(),
                        $"ResolveWearableByPointerSystem: Null pointer found in the list of pointers: index {index}"
                    );

                    continue;
                }

                URN shortenedPointer = loadingIntentionPointer;
                loadingIntentionPointer = shortenedPointer.Shorten();

                if (!wearableCatalog.TryGetWearable(loadingIntentionPointer, out IWearable wearable))
                {
                    wearableCatalog.AddEmptyWearable(loadingIntentionPointer);
                    missingPointers.Add(loadingIntentionPointer);
                    continue;
                }

                if (wearable.WearableDTO.Succeeded)
                {
                    successfulDtos++;
                    resolvedDTOs.Add(wearable);
                }
            }

            if (missingPointers.Count > 0)
            {
                CreateMissingPointersPromise(missingPointers, wearablesByPointersIntention, partitionComponent);
                return;
            }

            ref HideWearablesResolution hideWearablesResolution = ref wearablesByPointersIntention.HideWearablesResolution;

            if (successfulDtos == wearablesByPointersIntention.Pointers.Count)
            {
                if (hideWearablesResolution.VisibleWearables == null)
                    WearableComponentsUtils.ExtractVisibleWearables(wearablesByPointersIntention.BodyShape, resolvedDTOs, resolvedDTOs.Count, ref hideWearablesResolution);

                successfulResults += wearablesByPointersIntention.Pointers.Count - hideWearablesResolution.VisibleWearables!.Count;

                for (var i = 0; i < hideWearablesResolution.VisibleWearables!.Count; i++)
                {
                    IWearable visibleWearable = hideWearablesResolution.VisibleWearables[i];

                    if (visibleWearable.IsLoading) continue;
                    if (CreateAssetBundlePromiseIfRequired(visibleWearable, wearablesByPointersIntention, partitionComponent)) continue;

                    if (visibleWearable.WearableAssetResults[wearablesByPointersIntention.BodyShape].Results.All(static r => r is { Succeeded: true }))
                    {
                        successfulResults++;

                        // Reference must be added only once when the wearable is resolved
                        if (BitWiseUtils.TrySetBit(ref wearablesByPointersIntention.ResolvedWearablesIndices, i))

                            // We need to add a reference here, so it is not lost if the flow interrupts in between (i.e. before creating instances of CachedWearable)
                            visibleWearable.WearableAssetResults[wearablesByPointersIntention.BodyShape].AddReference();
                    }
                }
            }

            WearableComponentsUtils.WEARABLES_POOL.Release(resolvedDTOs);

            // If there are no missing pointers, we release the list
            WearableComponentsUtils.POINTERS_POOL.Release(missingPointers);

            if (successfulResults == wearablesByPointersIntention.Pointers.Count)
                World.Add(entity, new StreamableResult(new WearablesResolution(hideWearablesResolution.VisibleWearables, hideWearablesResolution.HiddenCategories)));
        }

        [Query]
        private void FinalizeWearableDTO([Data] bool defaultWearablesResolved, in Entity entity, ref AssetPromise<WearablesDTOList, GetWearableDTOByPointersIntention> promise, ref BodyShape bodyShape)
        {
            if (promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested)
            {
                foreach (string pointerID in promise.LoadingIntention.Pointers)
                {
                    wearableCatalog.TryGetWearable(pointerID, out IWearable component);
                    component.IsLoading = false;
                }

                promise.ForgetLoading(World);
                World.Destroy(entity);
                return;
            }

            if (promise.TryConsume(World, out StreamableLoadingResult<WearablesDTOList> promiseResult))
            {
                if (!promiseResult.Succeeded)
                {
                    foreach (string pointerID in promise.LoadingIntention.Pointers)
                    {
                        wearableCatalog.TryGetWearable(pointerID, out IWearable component);
                        SetDefaultWearables(defaultWearablesResolved, component, in bodyShape);
                        component.IsLoading = false;
                    }
                }
                else
                {
                    foreach (WearableDTO assetEntity in promiseResult.Asset.Value)
                    {
                        wearableCatalog.TryGetWearable(assetEntity.metadata.id, out IWearable component);

                        component.ResolveDTO(new StreamableLoadingResult<WearableDTO>(assetEntity));
                        component.IsLoading = false;
                    }
                }

                WearableComponentsUtils.POINTERS_POOL.Release(promise.LoadingIntention.Pointers);
                World.Destroy(entity);
            }
        }

        [Query]
        private void FinalizeAssetBundleManifestLoading([Data] bool defaultWearablesResolved, in Entity entity, ref AssetBundleManifestPromise promise, ref IWearable wearable, ref BodyShape bodyShape,
            ref IPartitionComponent partitionComponent)
        {
            if (promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested)
            {
                wearable.IsLoading = false;
                wearable.ManifestResult = null;
                promise.ForgetLoading(World);
                World.Destroy(entity);
                return;
            }

            if (promise.TryConsume(World, out StreamableLoadingResult<SceneAssetBundleManifest> result))
            {
                if (result.Succeeded)
                    wearable.ManifestResult = result;
                else
                    SetDefaultWearables(defaultWearablesResolved, wearable, in bodyShape);

                wearable.IsLoading = false;
                WearableComponentsUtils.CreateWearableThumbnailPromiseAB(assetBundleURL, wearable, World, partitionComponent).Forget();
                World.Destroy(entity);
            }
        }

        [Query]
        private void FinalizeAssetBundleLoading([Data] bool defaultWearablesResolved, in Entity entity, ref AssetBundlePromise promise, ref IWearable wearable, in BodyShape bodyShape, int index)
        {
            if (promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested)
            {
                ResetWearableResultOnCancellation(wearable, in bodyShape, index);
                promise.ForgetLoading(World);
                World.Destroy(entity);
                return;
            }

            if (promise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> result))
            {
                // every asset in the batch is mandatory => if at least one has already failed set the default wearables
                if (result.Succeeded && !AnyAssetHasFailed(wearable, bodyShape))
                    SetWearableResult(wearable, result, in bodyShape, index);
                else
                    SetDefaultWearables(defaultWearablesResolved, wearable, in bodyShape);

                wearable.IsLoading = !AllAssetsAreLoaded(wearable, bodyShape);
                World.Destroy(entity);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool AllAssetsAreLoaded(IWearable wearable, BodyShape bodyShape)
            => wearable.WearableAssetResults[bodyShape].Results.All(static r => r is { IsInitialized: true });

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool AnyAssetHasFailed(IWearable wearable, BodyShape bodyShape) =>
            wearable.WearableAssetResults[bodyShape].ReplacedWithDefaults;

        private bool CreateAssetBundlePromiseIfRequired(IWearable component, in GetWearablesByPointersIntention intention, IPartitionComponent partitionComponent)
        {
            // Manifest is required for Web loading only
            if (component.ManifestResult == null && EnumUtils.HasFlag(intention.PermittedSources, AssetSource.WEB))
                return component.CreateAssetBundleManifestPromise(World, intention.BodyShape, intention.CancellationTokenSource, partitionComponent);

            if (component.TryCreateAssetBundlePromise(in intention, customStreamingSubdirectory, partitionComponent, World))
            {
                component.IsLoading = true;
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
                ReportHub.LogError(GetReportCategory(), $"Default wearable {wearable.GetHash()} failed to load");

                StreamableLoadingResult<WearableAssetBase> failedResult = new StreamableLoadingResult<AssetBundleData>(new Exception("Default wearable failed to load"))
                   .ToWearableAsset(wearable);

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
                    result.Results ??= new StreamableLoadingResult<WearableAssetBase>?[1]; // default capacity, can't tell without the DTO
                    result.ReplacedWithDefaults = true;
                    result.Results[0] = failedResult;
                }
            }

            ReportHub.Log(GetReportCategory(), $"Request for wearable with hash {wearable.GetHash()} and urn {wearable.GetUrn()} failed, loading default wearable");

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
                IWearable defaultWearable = wearableCatalog.GetDefaultWearable(bs, wearable.GetCategory());
                var defaultWearableResults = defaultWearable.WearableAssetResults[bs];

                // the destination array might be not created if DTO itself has failed to load
                ref var result = ref wearable.WearableAssetResults[bs];
                result.Results ??= new StreamableLoadingResult<WearableAssetBase>?[defaultWearableResults.Results.Length];
                result.ReplacedWithDefaults = true;

                Array.Copy(defaultWearableResults.Results, result.Results, defaultWearableResults.Results.Length);
            }
        }

        /// <summary>
        ///     If the loading of the asset was cancelled reset the promise so we can start downloading it again with a new intent
        /// </summary>
        private static void ResetWearableResultOnCancellation(IWearable wearable, in BodyShape bodyShape, int index)
        {
            wearable.IsLoading = false;

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
            StreamableLoadingResult<WearableAssetBase> wearableResult = result.ToWearableAsset(wearable);

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
