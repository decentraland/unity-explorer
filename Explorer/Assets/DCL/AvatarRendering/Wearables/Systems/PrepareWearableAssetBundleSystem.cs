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
    public partial class PrepareWearableAssetBundleSystem : BaseUnityLoopSystem
    {
        private SingleInstanceEntity wearableCatalog;

        public PrepareWearableAssetBundleSystem(World world) : base(world) { }

        public override void Initialize()
        {
            base.Initialize();
            wearableCatalog = World.CacheWearableCatalog();
        }

        protected override void Update(float t)
        {
            PrepareWearableAssetBundleManifestDownloadingQuery(World);
            FinalizeAssetBundleManifestLoadingQuery(World);
            FinalizeAssetBundleLoadingQuery(World);
        }

        [Query]
        private void PrepareWearableAssetBundleManifestDownloading(ref WearableComponent wearableComponent)
        {
            if (wearableComponent.AssetBundleStatus == WearableComponent.AssetBundleLifeCycle.AssetBundleRequested)
            {
                //TODO: The URL is resolved in the DownloadAssetBundleManifestSystem. Should a prepare system be done?
                wearableComponent.wearableAssetBundleManifestPromise =
                    AssetPromise<SceneAssetBundleManifest, GetWearableAssetBundleManifestIntention>.Create(World,
                        new GetWearableAssetBundleManifestIntention
                        {
                            CommonArguments = new CommonLoadingArguments(wearableComponent.hash),
                            Hash = wearableComponent.hash,
                        },
                        PartitionComponent.TOP_PRIORITY);

                wearableComponent.AssetBundleStatus = WearableComponent.AssetBundleLifeCycle.AssetBundleManifestLoading;
            }
        }



        [Query]
        private void FinalizeAssetBundleManifestLoading(ref WearableComponent wearableComponent)
        {
            if (wearableComponent.AssetBundleStatus == WearableComponent.AssetBundleLifeCycle.AssetBundleManifestLoading &&
                wearableComponent.wearableAssetBundleManifestPromise.TryConsume(World, out StreamableLoadingResult<SceneAssetBundleManifest> result))
            {
                if (result.Succeeded)
                {
                    //TODO: I dont like the idea of adding the GetPlatform here, maybe move it to a PrepareSystem?
                    wearableComponent.wearableAssetBundlePromise =
                        AssetPromise<AssetBundleData, GetWearableAssetBundleIntention>.Create(World,
                            GetWearableAssetBundleIntention.FromHash(result.Asset, wearableComponent.GetMainFileHash() + PlatformUtils.GetPlatform()),
                            PartitionComponent.TOP_PRIORITY);

                    wearableComponent.AssetBundleStatus = WearableComponent.AssetBundleLifeCycle.AssetBundleLoading;
                }
                else
                {
                    SetDefaultWearable(ref wearableComponent);
                    wearableComponent.AssetBundleStatus = WearableComponent.AssetBundleLifeCycle.AssetBundleLoaded;
                }
            }
        }

        [Query]
        private void FinalizeAssetBundleLoading(ref WearableComponent wearableComponent)
        {
            if (wearableComponent.AssetBundleStatus == WearableComponent.AssetBundleLifeCycle.AssetBundleLoading
                && wearableComponent.wearableAssetBundlePromise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> result))
            {
                if (result.Succeeded)
                    wearableComponent.AssetBundleData = result.Asset;
                else
                    SetDefaultWearable(ref wearableComponent);

                wearableComponent.AssetBundleStatus = WearableComponent.AssetBundleLifeCycle.AssetBundleLoaded;
            }
        }

        private void SetDefaultWearable(ref WearableComponent wearableComponent)
        {
            ReportHub.LogError(GetReportCategory(), $"Asset bundle for wearable: {wearableComponent.hash} failed, loading default asset bundle");

            string defaultWearableUrn
                = WearablesLiterals.DefaultWearables.GetDefaultWearable(WearablesLiterals.BodyShapes.DEFAULT, wearableComponent.wearableContent.category);

            wearableComponent.AssetBundleData = World.Get<WearableComponent>(wearableCatalog.GetWearableCatalog(World).catalog[defaultWearableUrn]).AssetBundleData;
        }

    }
}
