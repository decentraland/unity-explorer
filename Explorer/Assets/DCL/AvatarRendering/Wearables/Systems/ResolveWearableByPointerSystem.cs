using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using Diagnostics.ReportsHandling;
using ECS;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using UnityEngine.Pool;
using AssetBundleManifestPromise = ECS.StreamableLoading.Common.AssetPromise<SceneRunner.Scene.SceneAssetBundleManifest, DCL.AvatarRendering.Wearables.Components.GetWearableAssetBundleManifestIntention>;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(PrepareGlobalAssetBundleLoadingParametersSystem))]
    public partial class ResolveWearableByPointerSystem : BaseUnityLoopSystem
    {
        private readonly WearableCatalog wearableCatalog;
        private readonly IRealmData realmData;

        public ResolveWearableByPointerSystem(World world, WearableCatalog wearableCatalog, IRealmData realmData) : base(world)
        {
            this.wearableCatalog = wearableCatalog;
            this.realmData = realmData;
        }

        protected override void Update(float t)
        {
            // Wait until the realm is configured
            if (!realmData.Configured) return;

            ResolveWearablePromiseQuery(World);
            FinalizeWearableDTOQuery(World);
            FinalizeAssetBundleManifestLoadingQuery(World);
            FinalizeAssetBundleLoadingQuery(World);
        }

        [Query]
        [None(typeof(StreamableLoadingResult<IWearable[]>))]
        private void ResolveWearablePromise(in Entity entity, ref GetWearablesByPointersIntention wearablesByPointersIntention, ref IPartitionComponent partitionComponent)
        {
            if (wearablesByPointersIntention.CancellationTokenSource.IsCancellationRequested)
            {
                World.Add(entity, new StreamableLoadingResult<IWearable[]>(new Exception("Pointer request cancelled")));
                return;
            }

            List<string> missingPointers = ListPool<string>.Get();
            var successfulResults = 0;

            for (var index = 0; index < wearablesByPointersIntention.Pointers.Count; index++)
            {
                string loadingIntentionPointer = wearablesByPointersIntention.Pointers[index];

                if (!wearableCatalog.TryGetWearable(loadingIntentionPointer, out IWearable component))
                {
                    wearableCatalog.AddEmptyWearable(loadingIntentionPointer);
                    missingPointers.Add(loadingIntentionPointer);
                    continue;
                }

                if (component.IsLoading) continue;

                if (RequiresComponentPromise(component, wearablesByPointersIntention, partitionComponent)) continue;

                if (component.AssetBundleData[wearablesByPointersIntention.BodyShape] is { Succeeded: true })
                {
                    successfulResults++;
                    wearablesByPointersIntention.Results[index] = component;
                }
            }

            if (missingPointers.Count > 0)
            {
                CreateMissingPointersPromise(missingPointers, wearablesByPointersIntention, partitionComponent);
                return;
            }

            //If there are no missing pointers, we release the list
            ListPool<string>.Release(missingPointers);

            if (successfulResults == wearablesByPointersIntention.Pointers.Count)
                World.Add(entity, new StreamableLoadingResult<IWearable[]>(wearablesByPointersIntention.Results));
        }

        [Query]
        private void FinalizeWearableDTO(in Entity entity, ref AssetPromise<WearableDTO[], GetWearableDTOByPointersIntention> promise, ref WearablesLiterals.BodyShape bodyShape)
        {
            if (promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested)
            {
                promise.ForgetLoading(World);
                World.Destroy(entity);
                return;
            }

            if (promise.TryConsume(World, out StreamableLoadingResult<WearableDTO[]> promiseResult))
            {
                if (!promiseResult.Succeeded)
                {
                    foreach (string pointerID in promise.LoadingIntention.Pointers)
                    {
                        wearableCatalog.TryGetWearable(pointerID, out IWearable component);
                        SetDefaultWearables(component, in bodyShape);
                        component.IsLoading = false;
                    }
                }
                else
                {
                    foreach (WearableDTO assetEntity in promiseResult.Asset)
                    {
                        //TODO: Download Thumbnail
                        wearableCatalog.TryGetWearable(assetEntity.metadata.id, out IWearable component);
                        component.WearableDTO = new StreamableLoadingResult<WearableDTO>(assetEntity);
                        component.IsLoading = false;
                    }
                }
                ListPool<string>.Release(promise.LoadingIntention.Pointers);
                World.Destroy(entity);
            }
        }

        [Query]
        private void FinalizeAssetBundleManifestLoading(in Entity entity, ref AssetBundleManifestPromise promise, ref IWearable wearable, ref WearablesLiterals.BodyShape bodyShape)
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
                    SetDefaultWearables(wearable, in bodyShape);

                wearable.IsLoading = false;
                World.Destroy(entity);
            }
        }

        [Query]
        private void FinalizeAssetBundleLoading(in Entity entity, ref AssetBundlePromise promise, ref IWearable wearable, ref WearablesLiterals.BodyShape bodyShape)
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
                    SetDefaultWearables(wearable, in bodyShape);

                wearable.IsLoading = false;
                World.Destroy(entity);
            }
        }

        private bool RequiresComponentPromise(IWearable component, GetWearablesByPointersIntention intention, IPartitionComponent partitionComponent)
        {
            if (component.ManifestResult == null)
            {
                var promise = AssetBundleManifestPromise.Create(World,
                    new GetWearableAssetBundleManifestIntention
                    {
                        Hash = component.GetHash(),

                        //TODO: Is it okay to use the original cancellation token source?
                        CommonArguments = new CommonLoadingArguments(component.GetHash(), cancellationTokenSource: intention.CancellationTokenSource),
                    },
                    partitionComponent);

                component.ManifestResult = new StreamableLoadingResult<SceneAssetBundleManifest>();
                component.IsLoading = true;
                World.Create(promise, component, intention.BodyShape);
                return true;
            }

            if (component.AssetBundleData[intention.BodyShape] == null && component.ManifestResult.Value.Asset != null)
            {
                var promise = AssetBundlePromise.Create(World,
                    GetAssetBundleIntention.FromHash(component.GetMainFileHash(intention.BodyShape) + PlatformUtils.GetPlatform(), manifest: component.ManifestResult.Value.Asset, cancellationTokenSource: intention.CancellationTokenSource),
                    partitionComponent);

                component.IsLoading = true;
                World.Create(promise, component, intention.BodyShape);
                return true;
            }

            return false;
        }

        private void CreateMissingPointersPromise(List<string> missingPointers, GetWearablesByPointersIntention intention, IPartitionComponent partitionComponent)
        {
            var wearableDtoByPointersIntention = new GetWearableDTOByPointersIntention
            {
                Pointers = missingPointers,
                CommonArguments = new CommonLoadingArguments(realmData.Ipfs.EntitiesActiveEndpoint, cancellationTokenSource: intention.CancellationTokenSource),
            };

            var promise = AssetPromise<WearableDTO[], GetWearableDTOByPointersIntention>.Create(World, wearableDtoByPointersIntention, partitionComponent);
            World.Create(promise, intention.BodyShape);
        }

        private void SetDefaultWearables(IWearable wearable, in WearablesLiterals.BodyShape bodyShape)
        {
            ReportHub.Log(GetReportCategory(), $"Request for wearable {wearable.GetHash()} failed, loading default wearable");

            //TODO: This section assumes that the default wearables were successfully loaded.
            //Waiting for the default wearable should be moved to the default screen
            if (wearable.IsUnisex())
            {
                wearable.AssetBundleData[WearablesLiterals.BodyShape.MALE] = wearableCatalog.GetDefaultWearable(WearablesLiterals.BodyShape.MALE, wearable.GetCategory()).AssetBundleData[WearablesLiterals.BodyShape.MALE];
                wearable.AssetBundleData[WearablesLiterals.BodyShape.FEMALE] = wearableCatalog.GetDefaultWearable(WearablesLiterals.BodyShape.FEMALE, wearable.GetCategory()).AssetBundleData[WearablesLiterals.BodyShape.FEMALE];
            }
            else
                wearable.AssetBundleData[bodyShape] = wearableCatalog.GetDefaultWearable(bodyShape, wearable.GetCategory()).AssetBundleData[bodyShape];
        }

        private void SetWearableResult(IWearable wearable, StreamableLoadingResult<AssetBundleData> result, in WearablesLiterals.BodyShape bodyShape)
        {
            if (wearable.IsUnisex())
            {
                wearable.AssetBundleData[WearablesLiterals.BodyShape.MALE] = result;
                wearable.AssetBundleData[WearablesLiterals.BodyShape.FEMALE] = result;
            }
            else
                wearable.AssetBundleData[bodyShape] = result;
        }
    }
}
