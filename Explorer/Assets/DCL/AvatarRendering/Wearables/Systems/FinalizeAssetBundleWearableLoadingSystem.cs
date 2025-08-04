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
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using AssetBundleManifestPromise = ECS.StreamableLoading.Common.AssetPromise<SceneRunner.Scene.SceneAssetBundleManifest, DCL.AvatarRendering.Wearables.Components.GetWearableAssetBundleManifestIntention>;
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
            FinalizeAssetBundleManifestLoadingQuery(World);
            FinalizeAssetBundleLoadingQuery(World);
        }

        [Query]
        private void FinalizeAssetBundleManifestLoading(Entity entity, ref AssetBundleManifestPromise promise, ref IWearable wearable, ref BodyShape bodyShape)
        {
            if (promise.IsCancellationRequested(World!))
            {
                wearable.ResetManifest();
                World.Destroy(entity);
                return;
            }

            if (promise.SafeTryConsume(World, GetReportCategory(), out StreamableLoadingResult<SceneAssetBundleManifest> result))
            {
                if (result.Succeeded)
                {
                    AssetValidation.ValidateSceneAssetBundleManifest(result.Asset, AssetValidation.SceneIDError, result.Asset.GetSceneID());
                    wearable.ManifestResult = result;
                }
                else
                    SetAsFailed(wearable, in bodyShape);

                wearable.UpdateLoadingStatus(false);
                World.Destroy(entity);
            }
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
            FinalizeAssetLoading<AssetBundleData, GetAssetBundleIntention>(entity, ref promise, wearable, in bodyShape, index, result => result.ToWearableAsset(wearable));
        }
    }
}
