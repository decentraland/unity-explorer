using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Wearables.Components;
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
    public partial class WearablePromiseSystem : BaseUnityLoopSystem
    {
        //TODO: Create a cache for the catalog
        private readonly Dictionary<string, Wearable> wearableCatalog;
        private string WEARABLE_CONTENT_BASE_URL;
        private readonly string WEARABLE_ENTITIES_URL;

        public WearablePromiseSystem(World world, Dictionary<string, Wearable> wearableCatalog, string wearableEntitiesURL) : base(world)
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

        //TODO: Why cant I use IPartitionComponent here?
        [Query]
        public void ResolveWearablePromise(in Entity entity, ref GetWearableByPointersIntention intention, ref PartitionComponent partitionComponent)
        {
            var successfullResults = 0;
            var missingPointers = new List<string>();

            for (var index = 0; index < intention.Pointers.Length; index++)
            {
                string loadingIntentionPointer = intention.Pointers[index];

                if (!wearableCatalog.TryGetValue(loadingIntentionPointer, out Wearable component))
                {
                    var wearable = new Wearable(loadingIntentionPointer);
                    wearable.IsLoading = true;
                    wearableCatalog.Add(loadingIntentionPointer, wearable);
                    missingPointers.Add(loadingIntentionPointer);
                }
                else
                {
                    if (component.IsLoading)
                        continue;

                    if (component.AssetBundleData[intention.BodyShape] is { Succeeded: true })
                    {
                        successfullResults++;
                        intention.results[index] = component;
                    }
                    else if (component.ManifestResult == null)
                    {
                        var promise = AssetBundleManifestPromise.Create(World,
                            new GetWearableAssetBundleManifestIntention
                            {
                                Hash = component.GetHash(),

                                //TODO: Resolving a url here to avoid the irrecoverable issue failure
                                CommonArguments = new CommonLoadingArguments(component.GetHash()),
                                BodyShape = intention.BodyShape,
                            },
                            partitionComponent);

                        component.ManifestResult = new StreamableLoadingResult<SceneAssetBundleManifest>();
                        component.IsLoading = true;
                        World.Create(promise, component);
                    }
                    else if (component.AssetBundleData[intention.BodyShape] == null)
                    {
                        var promise = AssetBundlePromise.Create(World,
                            GetWearableAssetBundleIntention.FromHash(component.ManifestResult.Value.Asset, component.GetMainFileHash(intention.BodyShape) + PlatformUtils.GetPlatform(), intention.BodyShape),
                            partitionComponent);

                        component.IsLoading = true;
                        World.Create(promise, component);
                    }
                }
            }

            if (missingPointers.Count > 0)
            {
                intention.CommonArguments = new CommonLoadingArguments(WEARABLE_ENTITIES_URL);
                var promise = AssetPromise<WearableDTO[], GetWearableByPointersIntention>.Create(World, intention, partitionComponent);

                World.Create(promise);
                return;
            }

            if (successfullResults == intention.Pointers.Length)
                World.Add(entity, new StreamableLoadingResult<Wearable[]>(intention.results));
        }

        [Query]
        public void FinalizeWearableDTO(in Entity entity, ref AssetPromise<WearableDTO[], GetWearableByPointersIntention> promise)
        {
            if (promise.TryGetResult(World, out StreamableLoadingResult<WearableDTO[]> promiseResult))
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
                    wearable.AssetBundleData[promise.LoadingIntention.BodyShape] = result;
                else
                    SetDefaultWearables(wearable, promise.LoadingIntention.BodyShape);

                wearable.IsLoading = false;
                World.Destroy(entity);
            }
        }

        private void SetDefaultWearables(Wearable component, string bodyShape)
        {
            ReportHub.Log(GetReportCategory(), $"Request for wearable {component.GetHash()} failed, loading default wearable");

            //TODO: This section assumes that the default wearables were successfully loaded.
            //Waiting for the default wearable should be moved to the default screen
            component.AssetBundleData[bodyShape] = wearableCatalog[WearablesLiterals.DefaultWearables.GetDefaultWearable(bodyShape, component.GetCategory())].AssetBundleData[bodyShape];
        }
    }
}
