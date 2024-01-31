﻿using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Components.Intentions;
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
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using AssetBundleManifestPromise = ECS.StreamableLoading.Common.AssetPromise<SceneRunner.Scene.SceneAssetBundleManifest, DCL.AvatarRendering.Wearables.Components.GetWearableAssetBundleManifestIntention>;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(PrepareGlobalAssetBundleLoadingParametersSystem))]
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class ResolveWearableByPointerSystem : BaseUnityLoopSystem
    {
        private readonly URLSubdirectory customStreamingSubdirectory;
        private readonly IRealmData realmData;
        private readonly IWearableCatalog wearableCatalog;

        private SingleInstanceEntity defaultWearablesState;

        public ResolveWearableByPointerSystem(World world, IWearableCatalog wearableCatalog, IRealmData realmData,
            URLSubdirectory customStreamingSubdirectory) : base(world)
        {
            this.wearableCatalog = wearableCatalog;
            this.realmData = realmData;
            this.customStreamingSubdirectory = customStreamingSubdirectory;
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
        [None(typeof(StreamableLoadingResult<IWearable[]>))]
        private void ResolveWearablePromise([Data] bool defaultWearablesResolved, in Entity entity, ref GetWearablesByPointersIntention wearablesByPointersIntention, ref IPartitionComponent partitionComponent)
        {
            if (wearablesByPointersIntention.CancellationTokenSource.IsCancellationRequested)
            {
                World.Add(entity, new StreamableLoadingResult<IWearable[]>(new Exception("Pointer request cancelled")));
                return;
            }

            // Instead of checking particular resolution, for simplicity just check if the default wearables are resolved
            // if it is required
            if (wearablesByPointersIntention.FallbackToDefaultWearables && !defaultWearablesResolved)
                return; // Wait for default wearables to be resolved

            List<string> missingPointers = WearableComponentsUtils.POINTERS_POOL.Get();
            List<IWearable> resolvedWereables = new List<IWearable>();
            var successfulResults = 0;
            var successfulDtos = 0;

            for (var index = 0; index < wearablesByPointersIntention.Pointers.Count; index++)
            {
                string loadingIntentionPointer = wearablesByPointersIntention.Pointers[index];

                if (!wearableCatalog.TryGetWearable(loadingIntentionPointer, out IWearable wearable))
                {
                    wearableCatalog.AddEmptyWearable(loadingIntentionPointer);
                    missingPointers.Add(loadingIntentionPointer);
                    continue;
                }

                if (wearable.WearableDTO.Succeeded)
                {
                    successfulDtos++;
                    resolvedWereables.Add(wearable);
                }

            }

            if (missingPointers.Count > 0)
            {
                CreateMissingPointersPromise(missingPointers, wearablesByPointersIntention, partitionComponent);
                return;
            }

            if (successfulDtos == wearablesByPointersIntention.Pointers.Count)
            {
                ref HideWearablesResolution hideWearablesResolution = ref wearablesByPointersIntention.HideWearablesResolution;

                if (hideWearablesResolution.VisibleWearables == null)
                    WearableComponentsUtils.ExtractVisibleWearables(wearablesByPointersIntention.BodyShape, resolvedWereables, resolvedWereables.Count, ref hideWearablesResolution);

                successfulResults += (wearablesByPointersIntention.Pointers.Count - hideWearablesResolution.VisibleWearables!.Count);
                for (int i = 0; i < hideWearablesResolution.VisibleWearables!.Count; i++)
                {
                    IWearable visibleWearable = hideWearablesResolution.VisibleWearables[i];

                    if (visibleWearable.IsLoading) continue;
                    if (CreateAssetBundlePromiseIfRequired(visibleWearable, wearablesByPointersIntention, partitionComponent)) continue;
                    if (visibleWearable.WearableAssetResults[wearablesByPointersIntention.BodyShape] is { Succeeded: true })
                    {
                        successfulResults++;

                        if (wearablesByPointersIntention.Results[i] == null)
                        {
                            // We need to add a reference here, so it is not lost if the flow interrupts in between (i.e. before creating instances of CachedWearable)
                            visibleWearable.WearableAssetResults[wearablesByPointersIntention.BodyShape].Value.Asset.AddReference();
                            wearablesByPointersIntention.Results[i] = visibleWearable;
                        }
                    }
                }
            }

            // If there are no missing pointers, we release the list
            WearableComponentsUtils.POINTERS_POOL.Release(missingPointers);

            if (successfulResults == wearablesByPointersIntention.Pointers.Count)
                World.Add(entity, new StreamableLoadingResult<IWearable[]>(wearablesByPointersIntention.Results));

        }

        [Query]
        private void FinalizeWearableDTO([Data] bool defaultWearablesResolved, in Entity entity, ref AssetPromise<WearablesDTOList, GetWearableDTOByPointersIntention> promise, ref BodyShape bodyShape, ref IPartitionComponent partitionComponent)
        {
            if (promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested)
            {
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

                        component.WearableDTO = new StreamableLoadingResult<WearableDTO>(assetEntity);
                        component.IsLoading = false;

                        WearableComponentsUtils.CreateWearableThumbnailPromise(realmData, component, World, partitionComponent);
                    }
                }

                WearableComponentsUtils.POINTERS_POOL.Release(promise.LoadingIntention.Pointers);
                World.Destroy(entity);
            }
        }

        [Query]
        private void FinalizeAssetBundleManifestLoading([Data] bool defaultWearablesResolved, in Entity entity, ref AssetBundleManifestPromise promise, ref IWearable wearable, ref BodyShape bodyShape)
        {
            if (promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested)
            {
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
                World.Destroy(entity);
            }
        }

        [Query]
        private void FinalizeAssetBundleLoading([Data] bool defaultWearablesResolved, in Entity entity, ref AssetBundlePromise promise, ref IWearable wearable, ref BodyShape bodyShape)
        {
            if (promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested)
            {
                promise.ForgetLoading(World);
                World.Destroy(entity);
                return;
            }

            if (promise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> result))
            {
                if (result.Succeeded)
                    SetWearableResult(wearable, result, in bodyShape);
                else
                    SetDefaultWearables(defaultWearablesResolved, wearable, in bodyShape);

                wearable.IsLoading = false;
                World.Destroy(entity);
            }
        }

        private bool CreateAssetBundlePromiseIfRequired(IWearable component, in GetWearablesByPointersIntention intention, IPartitionComponent partitionComponent)
        {
            // Manifest is required for Web loading only
            if (component.ManifestResult == null && EnumUtils.HasFlag(intention.PermittedSources, AssetSource.WEB))
            {
                var promise = AssetBundleManifestPromise.Create(World,
                    new GetWearableAssetBundleManifestIntention(component.GetHash(), new CommonLoadingArguments(component.GetHash(), cancellationTokenSource: intention.CancellationTokenSource)),
                    partitionComponent);

                component.ManifestResult = new StreamableLoadingResult<SceneAssetBundleManifest>();
                component.IsLoading = true;
                World.Create(promise, component, intention.BodyShape);
                return true;
            }

            if (component.WearableAssetResults[intention.BodyShape] == null)
            {
                SceneAssetBundleManifest manifest = !EnumUtils.HasFlag(intention.PermittedSources, AssetSource.WEB) ? null : component.ManifestResult?.Asset;

                var promise = AssetBundlePromise.Create(World,
                    GetAssetBundleIntention.FromHash(component.GetMainFileHash(intention.BodyShape) + PlatformUtils.GetPlatform(),
                        permittedSources: intention.PermittedSources,
                        customEmbeddedSubDirectory: customStreamingSubdirectory,
                        manifest: manifest, cancellationTokenSource: intention.CancellationTokenSource),
                    partitionComponent);

                component.IsLoading = true;
                World.Create(promise, component, intention.BodyShape);
                return true;
            }

            return false;
        }

        private void CreateMissingPointersPromise(List<string> missingPointers, GetWearablesByPointersIntention intention, IPartitionComponent partitionComponent)
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
                ReportHub.LogError(GetReportCategory(), $"Default wearable {wearable.WearableDTO.Asset.id} failed to load");

                StreamableLoadingResult<WearableAsset> failedResult = new StreamableLoadingResult<AssetBundleData>(new Exception("Default wearable failed to load"))
                   .ToWearableAsset();

                if (wearable.IsUnisex())
                {
                    wearable.WearableAssetResults[BodyShape.MALE] = failedResult;
                    wearable.WearableAssetResults[BodyShape.FEMALE] = failedResult;
                }
                else
                    wearable.WearableAssetResults[bodyShape] = failedResult;

                return;
            }

            ReportHub.Log(GetReportCategory(), $"Request for wearable {wearable.GetHash()} failed, loading default wearable");

            var defaultWearable = wearableCatalog.GetDefaultWearable(bodyShape, wearable.GetCategory(),
                out var hasEmptyDefaultWearableAB);
            if (wearable.IsUnisex())
            {
                wearable.WearableAssetResults[BodyShape.MALE] = defaultWearable.WearableAssetResults[BodyShape.MALE];
                wearable.WearableAssetResults[BodyShape.FEMALE] =
                    defaultWearable.WearableAssetResults[BodyShape.FEMALE];
            }
            else
                wearable.WearableAssetResults[bodyShape] = defaultWearable.WearableAssetResults[bodyShape];

            wearable.WearableDTO.Asset.Sanitize(hasEmptyDefaultWearableAB);
        }

        private static void SetWearableResult(IWearable wearable, StreamableLoadingResult<AssetBundleData> result, in BodyShape bodyShape)
        {
            StreamableLoadingResult<WearableAsset> wearableResult = result.ToWearableAsset();

            if (wearable.IsUnisex())
            {
                wearable.WearableAssetResults[BodyShape.MALE] = wearableResult;
                wearable.WearableAssetResults[BodyShape.FEMALE] = wearableResult;
            }
            else
                wearable.WearableAssetResults[bodyShape] = wearableResult;
        }
    }
}
