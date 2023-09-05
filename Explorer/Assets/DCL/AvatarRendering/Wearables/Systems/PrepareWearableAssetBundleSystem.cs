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
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using AssetBundleManifestPromise = ECS.StreamableLoading.Common.AssetPromise<SceneRunner.Scene.SceneAssetBundleManifest, DCL.AvatarRendering.Wearables.Components.GetWearableAssetBundleManifestIntention>;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, DCL.AvatarRendering.Wearables.Components.GetWearableAssetBundleIntention>;


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
            PrepareWearableAssetBundleManifestLoadingQuery(World);
            FinalizeAssetBundleManifestLoadingQuery(World);
            PrepareWearableAssetBundleLoadingQuery(World);
            FinalizeAssetBundleLoadingQuery(World);
        }

        [Query]
        [All(typeof(WearableComponentsHelper.GetWearableAssetBundleManifestFlag))]
        [None(typeof(AssetBundleManifestPromise))]
        private void PrepareWearableAssetBundleManifestLoading(in Entity entity, ref WearableComponent wearableComponent)
        {
            //TODO: The URL is resolved in the DownloadAssetBundleManifestSystem. Should a prepare system be done?
            var assetPromise =
                AssetBundleManifestPromise.Create(World,
                    new GetWearableAssetBundleManifestIntention
                    {
                        CommonArguments = new CommonLoadingArguments(wearableComponent.hash),
                        Hash = wearableComponent.hash,
                    },
                    PartitionComponent.TOP_PRIORITY);

            World.Add(entity, assetPromise);
        }



        [Query]
        [All(typeof(WearableComponentsHelper.GetWearableAssetBundleManifestFlag))]
        [None(typeof(WearableComponentsHelper.GetWearableAssetBundleFlag))]
        private void FinalizeAssetBundleManifestLoading(in Entity entity, ref WearableComponent wearableComponent, ref AssetBundleManifestPromise promise)
        {
            if (promise.TryConsume(World, out StreamableLoadingResult<SceneAssetBundleManifest> result))
            {
                if (result.Succeeded)
                {
                    wearableComponent.AssetBundleManifest = result.Asset;
                    World.Add(entity, new WearableComponentsHelper.GetWearableAssetBundleFlag());
                }
                else
                    SetDefaultWearable(in entity, ref wearableComponent);
            }
        }

        [Query]
        [All(typeof(WearableComponentsHelper.GetWearableAssetBundleFlag))]
        [None(typeof(AssetBundlePromise))]
        private void PrepareWearableAssetBundleLoading(in Entity entity, ref WearableComponent wearableComponent)
        {
            var assetBundlePromise =
                AssetBundlePromise.Create(World,
                    GetWearableAssetBundleIntention.FromHash(wearableComponent.AssetBundleManifest, wearableComponent.GetMainFileHash() + PlatformUtils.GetPlatform()),
                    PartitionComponent.TOP_PRIORITY);

            World.Add(entity, assetBundlePromise);
        }

        [Query]
        [All(typeof(WearableComponentsHelper.GetWearableAssetBundleFlag))]
        private void FinalizeAssetBundleLoading(in Entity entity, ref WearableComponent wearableComponent,
            ref AssetBundlePromise promise)
        {
            if (promise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> result))
            {
                if (result.Succeeded)
                {
                    wearableComponent.AssetBundleData = result.Asset;
                    CleanEntity(in entity);
                }
                else
                    SetDefaultWearable(in entity, ref wearableComponent);
            }
        }

        private void CleanEntity(in Entity entity)
        {
            World.Remove<WearableComponentsHelper.GetWearableAssetBundleFlag, WearableComponentsHelper.GetWearableAssetBundleManifestFlag,
                AssetBundlePromise, AssetBundleManifestPromise>(entity);
        }

        private void SetDefaultWearable(in Entity entity, ref WearableComponent wearableComponent)
        {
            ReportHub.LogError(GetReportCategory(), $"Asset bundle for wearable: {wearableComponent.hash} failed, loading default asset bundle");

            string defaultWearableUrn
                = WearablesLiterals.DefaultWearables.GetDefaultWearable(WearablesLiterals.BodyShapes.DEFAULT, wearableComponent.wearableContent.category);

            wearableComponent.AssetBundleData = World.Get<WearableComponent>(wearableCatalog.GetWearableCatalog(World).catalog[defaultWearableUrn]).AssetBundleData;
            CleanEntity(entity);
        }

    }
}
