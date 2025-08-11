using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using ECS;
using ECS.StreamableLoading.AssetBundles;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(PrepareGlobalAssetBundleLoadingParametersSystem))]
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class FinalizeAssetBundleWearableLoadingSystem : FinalizeWearableLoadingSystemBase
    {
        public FinalizeAssetBundleWearableLoadingSystem(
            World world,
            IWearableStorage wearableStorage,
            IRealmData realmData
        ) : base(world, wearableStorage, realmData)
        {
        }

        protected override void Update(float t)
        {
            base.Update(t);

            // Asset Bundles can be Resolved with Embedded Data
            FinalizeAssetBundleLoadingQuery(World);
        }

        [Query]
        private void FinalizeAssetBundleLoading(
            Entity entity,
            ref AssetBundlePromise promise,
            IWearable wearable,
            in BodyShape bodyShape,
            int index
        )
        {
            FinalizeAssetLoading(entity, ref promise, wearable, in bodyShape, index, result => result.ToWearableAsset(wearable));
        }
    }
}
