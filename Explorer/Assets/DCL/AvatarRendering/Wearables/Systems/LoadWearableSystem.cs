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

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class LoadWearableSystem : BaseUnityLoopSystem
    {
        private readonly string WEARABLE_CONTENT_BASE_URL;

        private SingleInstanceEntity wearableCatalog;

        public LoadWearableSystem(World world, string wearableContentBaseURL) : base(world)
        {
            WEARABLE_CONTENT_BASE_URL = wearableContentBaseURL;
        }

        public override void Initialize()
        {
            base.Initialize();
            wearableCatalog = World.CacheWearableCatalog();
        }

        protected override void Update(float t)
        {
            CreateWearablesComponentFromResultQuery(World);
            FinalizeAssetBundleManifestLoadingQuery(World);
            FinalizeAssetBundleLoadingQuery(World);
        }

        [Query]
        private void CreateWearablesComponentFromResult(ref StreamableLoadingResult<WearableDTO[]> wearableDTOResult)
        {
            // If the result failed, the result will be handled by the system that requested the wearables
            if (!wearableDTOResult.Succeeded)
                return;

            foreach (WearableDTO assetEntity in wearableDTOResult.Asset)
            {
                WearableComponent wearableComponent = assetEntity.ToWearableItem(WEARABLE_CONTENT_BASE_URL);
                if (!wearableCatalog.GetWearableCatalog(World).catalog.ContainsKey(wearableComponent.urn))
                {
                    //TODO: Download Thumbnail
                    Entity wearableEntity = World.Create(wearableComponent);
                    wearableCatalog.GetWearableCatalog(World).catalog.Add(wearableComponent.urn, World.Reference(wearableEntity));
                }
            }
        }

        [Query]
        private void FinalizeAssetBundleManifestLoading(in Entity entity, ref WearableComponent wearableComponent)
        {
            if (wearableComponent.AssetBundleStatus == WearableComponent.AssetBundleLifeCycle.AssetBundleManifestLoading &&
                wearableComponent.wearableAssetBundleManifestPromise.TryConsume(World, out StreamableLoadingResult<SceneAssetBundleManifest> result))
            {
                if (!result.Succeeded)
                {
                    //TODO: Error flow, add a default asset bundle to avoid blocking the instantiation of the caller
                    World.Add(entity, new AssetBundleData(null, null, null));
                    ReportHub.LogError(GetReportCategory(), $"Asset bundle manifest for wearable: {wearableComponent.hash} failed, loading default asset bundle");
                    wearableComponent.AssetBundleStatus = WearableComponent.AssetBundleLifeCycle.AssetBundleLoaded;
                }
                else
                {
                    //TODO: I dont like the idea of adding the GetPlatform here, can it be moved to the PrepareSystem?
                    wearableComponent.wearableAssetBundlePromise =
                        AssetPromise<AssetBundleData, GetWearableAssetBundleIntention>.Create(World,
                            GetWearableAssetBundleIntention.FromHash(result.Asset, wearableComponent.GetMainFileHash() + PlatformUtils.GetPlatform()),
                            PartitionComponent.TOP_PRIORITY);
                    wearableComponent.AssetBundleStatus = WearableComponent.AssetBundleLifeCycle.AssetBundleLoading;
                }
            }
        }

        [Query]
        private void FinalizeAssetBundleLoading(in Entity entity, ref WearableComponent wearableComponent)
        {
            if (wearableComponent.AssetBundleStatus == WearableComponent.AssetBundleLifeCycle.AssetBundleLoading
                && wearableComponent.wearableAssetBundlePromise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> result))
            {
                wearableComponent.AssetBundleStatus = WearableComponent.AssetBundleLifeCycle.AssetBundleLoaded;

                if (!result.Succeeded)
                {
                    //TODO: Error flow, add a default asset bundle to avoid blocking the instantiation of the caller
                    World.Add(entity, new AssetBundleData(null, null, null));
                    ReportHub.LogError(GetReportCategory(), $"Asset bundle for wearable: {wearableComponent.hash} failed, loading default asset bundle");
                }

                else
                    World.Add(entity, result.Asset);
            }
        }

    }
}
