using AssetBundleManifestPromise = ECS.StreamableLoading.Common.AssetPromise<SceneRunner.Scene.SceneAssetBundleManifest, DCL.AvatarRendering.Wearables.Components.GetWearableAssetBundleManifestIntention>;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, DCL.AvatarRendering.Wearables.Components.GetWearableAssetBundleIntention>;


namespace DCL.AvatarRendering.Wearables.Systems
{
    /*
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
        private void PrepareWearableAssetBundleManifestLoading(in Entity entity, ref Wearable wearable)
        {
            //TODO: The URL is resolved in the DownloadAssetBundleManifestSystem. Should a prepare system be done?
            var assetPromise =
                AssetBundleManifestPromise.Create(World,
                    new GetWearableAssetBundleManifestIntention
                    {
                        CommonArguments = new CommonLoadingArguments(wearable.hash),
                        Hash = wearable.hash,
                    },
                    PartitionComponent.TOP_PRIORITY);

            World.Add(entity, assetPromise);
        }



        [Query]
        [All(typeof(WearableComponentsHelper.GetWearableAssetBundleManifestFlag))]
        [None(typeof(WearableComponentsHelper.GetWearableAssetBundleFlag))]
        private void FinalizeAssetBundleManifestLoading(in Entity entity, ref Wearable wearable, ref AssetBundleManifestPromise promise)
        {
            if (promise.TryConsume(World, out StreamableLoadingResult<SceneAssetBundleManifest> result))
            {
                if (result.Succeeded)
                {
                    wearable.AssetBundleManifest = result.Asset;
                    World.Add(entity, new WearableComponentsHelper.GetWearableAssetBundleFlag());
                }
                else
                    SetDefaultWearable(in entity, ref wearable);
            }
        }

        [Query]
        [All(typeof(WearableComponentsHelper.GetWearableAssetBundleFlag))]
        [None(typeof(AssetBundlePromise))]
        private void PrepareWearableAssetBundleLoading(in Entity entity, ref Wearable wearable)
        {
            var assetBundlePromise =
                AssetBundlePromise.Create(World,
                    GetWearableAssetBundleIntention.FromHash(wearable.AssetBundleManifest, wearable.GetMainFileHash() + PlatformUtils.GetPlatform()),
                    PartitionComponent.TOP_PRIORITY);

            World.Add(entity, assetBundlePromise);
        }

        [Query]
        [All(typeof(WearableComponentsHelper.GetWearableAssetBundleFlag))]
        private void FinalizeAssetBundleLoading(in Entity entity, ref Wearable wearable,
            ref AssetBundlePromise promise)
        {
            if (promise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> result))
            {
                if (result.Succeeded)
                {
                    wearable.AssetBundleData = result.Asset;
                    CleanEntity(in entity);
                }
                else
                    SetDefaultWearable(in entity, ref wearable);
            }
        }

        private void CleanEntity(in Entity entity)
        {
            World.Remove<WearableComponentsHelper.GetWearableAssetBundleFlag, WearableComponentsHelper.GetWearableAssetBundleManifestFlag,
                AssetBundlePromise, AssetBundleManifestPromise>(entity);
        }

        private void SetDefaultWearable(in Entity entity, ref Wearable wearable)
        {
            ReportHub.LogError(GetReportCategory(), $"Asset bundle for wearable: {wearable.hash} failed, loading default asset bundle");

            string defaultWearableUrn
                = WearablesLiterals.DefaultWearables.GetDefaultWearable(WearablesLiterals.BodyShapes.DEFAULT, wearable.wearableContent.category);

            wearable.AssetBundleData = World.Get<Wearable>(wearableCatalog.GetWearableCatalog(World).catalog[defaultWearableUrn]).AssetBundleData;
            CleanEntity(entity);
        }

    }
    */
}
