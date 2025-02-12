using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using ECS;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using Utility;
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
            IRealmData realmData,
            URLSubdirectory customStreamingSubdirectory
        ) : base(world, wearableStorage, realmData, customStreamingSubdirectory)
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
        protected void FinalizeAssetBundleManifestLoading(Entity entity, ref AssetBundleManifestPromise promise, ref IWearable wearable, ref BodyShape bodyShape)
        {
            if (promise.TryForgetWithEntityIfCancelled(entity, World!))
            {
                wearable.ResetManifest();
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
                    SetDefaultWearables(defaultWearablesResolved, wearable, in bodyShape);

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

        protected override bool CreateAssetPromiseIfRequired(IWearable component, in GetWearablesByPointersIntention intention, IPartitionComponent partitionComponent)
        {
            // Do not repeat the promise if already failed once. Otherwise it will end up in an endless loading:true state
            if (component.ManifestResult is { Succeeded: false }) return false;

            // Manifest is required for Web loading only
            if (component.ManifestResult == null && EnumUtils.HasFlag(intention.PermittedSources, AssetSource.WEB))
                return component.CreateAssetBundleManifestPromise(World, intention.BodyShape, intention.CancellationTokenSource, partitionComponent);

            if (component.TryCreateAssetPromise(in intention, customStreamingSubdirectory, partitionComponent, World, GetReportCategory()))
            {
                component.UpdateLoadingStatus(true);
                return true;
            }

            return false;
        }
    }
}
