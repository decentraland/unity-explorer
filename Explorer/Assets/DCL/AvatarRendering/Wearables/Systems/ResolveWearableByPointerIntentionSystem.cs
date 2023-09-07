using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using Diagnostics.ReportsHandling;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System.Collections.Generic;
using AssetBundleManifestPromise = ECS.StreamableLoading.Common.AssetPromise<SceneRunner.Scene.SceneAssetBundleManifest, DCL.AvatarRendering.Wearables.Components.GetWearableAssetBundleManifestIntention>;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, DCL.AvatarRendering.Wearables.Components.GetWearableAssetBundleIntention>;

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(PrepareWearableAssetBundleLoadingParametersSystem))]
    public partial class ResolveWearableByPointerIntentionSystem : BaseUnityLoopSystem
    {
        private readonly Dictionary<string, Wearable> wearableCatalog;
        private string WEARABLE_CONTENT_BASE_URL;
        private readonly string WEARABLE_ENTITIES_URL;

        public ResolveWearableByPointerIntentionSystem(World world, Dictionary<string, Wearable> wearableCatalog, string wearableEntitiesURL) : base(world)
        {
            this.wearableCatalog = wearableCatalog;
            WEARABLE_ENTITIES_URL = wearableEntitiesURL;
        }

        protected override void Update(float t)
        {
            ResolveWearablePromiseQuery(World);
            FinalizeWearableDTOQuery(World);
            FinalizeAssetBundleManifestLoadingQuery(World);
            FinalizeAssetBundleLoadingQuery(World);
        }

        [Query]
        [None(typeof(Wearable[]))]
        public void ResolveWearablePromise(in Entity entity, ref GetWearablesByPointersIntention wearablesByPointersIntention, ref PartitionComponent partitionComponent)
        {
            var resolvedWearables = new List<Wearable>();
            var missingPointers = new List<string>();

            for (var index = 0; index < wearablesByPointersIntention.Pointers.Length; index++)
            {
                string loadingIntentionPointer = wearablesByPointersIntention.Pointers[index];
                if (!wearableCatalog.TryGetValue(loadingIntentionPointer, out Wearable component))
                {
                    wearableCatalog.Add(loadingIntentionPointer, new Wearable(loadingIntentionPointer));
                    missingPointers.Add(loadingIntentionPointer);
                }
                else
                {
                    if (component.IsLoading)
                        continue;

                    if (component.AssetBundleData[wearablesByPointersIntention.BodyShape] is { Succeeded: true })
                        resolvedWearables.Add(component);
                    else if (component.ManifestResult == null)
                    {
                        var promise = AssetBundleManifestPromise.Create(World,
                            new GetWearableAssetBundleManifestIntention
                            {
                                Hash = component.GetHash(),
                                //TODO: Resolving a url here to avoid the irrecoverable issue failure
                                CommonArguments = new CommonLoadingArguments(component.GetHash()),
                                BodyShape = wearablesByPointersIntention.BodyShape,
                            },
                            partitionComponent);

                        component.ManifestResult = new StreamableLoadingResult<SceneAssetBundleManifest>();
                        component.IsLoading = true;
                        World.Create(promise, component);
                    }
                    else if (component.AssetBundleData[wearablesByPointersIntention.BodyShape] == null && component.ManifestResult.Value.Asset != null)
                    {
                        var promise = AssetBundlePromise.Create(World,
                            GetWearableAssetBundleIntention.FromHash(component.ManifestResult.Value.Asset, component.GetMainFileHash(wearablesByPointersIntention.BodyShape) + PlatformUtils.GetPlatform(), wearablesByPointersIntention.BodyShape),
                            partitionComponent);

                        component.IsLoading = true;
                        World.Create(promise, component);
                    }
                }
            }

            if (missingPointers.Count > 0)
            {
                var wearableDtoByPointersIntention
                    = new GetWearableDTOByPointersIntention
                    {
                        Pointers = missingPointers.ToArray(),
                        BodyShape = wearablesByPointersIntention.BodyShape,
                        CommonArguments = new CommonLoadingArguments(WEARABLE_ENTITIES_URL),
                    };

                var promise = AssetPromise<WearableDTO[], GetWearableDTOByPointersIntention>.Create(World, wearableDtoByPointersIntention, partitionComponent);
                World.Create(promise);
                return;
            }

            if (resolvedWearables.Count == wearablesByPointersIntention.Pointers.Length)
                World.Add(entity, resolvedWearables.ToArray());
        }


        [Query]
        public void FinalizeWearableDTO(in Entity entity, ref AssetPromise<WearableDTO[], GetWearableDTOByPointersIntention> promise)
        {
            if (promise.TryConsume(World, out StreamableLoadingResult<WearableDTO[]> promiseResult))
            {
                if (!promiseResult.Succeeded)
                {
                    foreach (string pointerID in promise.LoadingIntention.Pointers)
                    {
                        Wearable component = wearableCatalog[pointerID];
                        SetDefaultWearables(component, promise.LoadingIntention.BodyShape);
                        component.IsLoading = false;
                    }
                }
                else
                {
                    foreach (WearableDTO assetEntity in promiseResult.Asset)
                    {
                        //TODO: Download Thumbnail
                        Wearable component = wearableCatalog[assetEntity.metadata.id];
                        component.WearableDTO = new StreamableLoadingResult<WearableDTO>(assetEntity);
                        component.IsLoading = false;
                    }
                }
                World.Destroy(entity);
            }
        }

        [Query]
        private void FinalizeAssetBundleManifestLoading(in Entity entity, ref AssetBundleManifestPromise promise, ref Wearable wearable)
        {
            if (promise.TryConsume(World, out StreamableLoadingResult<SceneAssetBundleManifest> result))
            {
                if (result.Succeeded)
                    wearable.ManifestResult = result;
                else
                    SetDefaultWearables(wearable, promise.LoadingIntention.BodyShape);

                wearable.IsLoading = false;
                World.Destroy(entity);
            }
        }

        [Query]
        private void FinalizeAssetBundleLoading(in Entity entity, ref AssetBundlePromise promise, ref Wearable wearable)
        {
            if (promise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> result))
            {
                if (result.Succeeded)
                    SetWearableResult(wearable, result, promise.LoadingIntention.BodyShape);
                else
                    SetDefaultWearables(wearable, promise.LoadingIntention.BodyShape);

                wearable.IsLoading = false;
                World.Destroy(entity);
            }
        }

        private void SetDefaultWearables(Wearable wearable, string bodyShape)
        {
            ReportHub.Log(GetReportCategory(), $"Request for wearable {wearable.GetHash()} failed, loading default wearable");
            //TODO: This section assumes that the default wearables were successfully loaded.
            //Waiting for the default wearable should be moved to the default screen

            if (wearable.IsUnisex())
            {
                wearable.AssetBundleData[WearablesLiterals.BodyShapes.MALE] = wearableCatalog[WearablesLiterals.DefaultWearables.GetDefaultWearable(WearablesLiterals.BodyShapes.MALE, wearable.GetCategory())].AssetBundleData[WearablesLiterals.BodyShapes.MALE];
                wearable.AssetBundleData[WearablesLiterals.BodyShapes.FEMALE] = wearableCatalog[WearablesLiterals.DefaultWearables.GetDefaultWearable(WearablesLiterals.BodyShapes.FEMALE, wearable.GetCategory())].AssetBundleData[WearablesLiterals.BodyShapes.FEMALE];
            }
            else
                wearable.AssetBundleData[bodyShape] = wearableCatalog[WearablesLiterals.DefaultWearables.GetDefaultWearable(bodyShape, wearable.GetCategory())].AssetBundleData[bodyShape];
        }

        private void SetWearableResult(Wearable wearable, StreamableLoadingResult<AssetBundleData> result, string bodyShape)
        {
            if (wearable.IsUnisex())
            {
                wearable.AssetBundleData[WearablesLiterals.BodyShapes.MALE] = result;
                wearable.AssetBundleData[WearablesLiterals.BodyShapes.FEMALE] = result;
            }
            else
                wearable.AssetBundleData[bodyShape] = result;
        }
    }
}
