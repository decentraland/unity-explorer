using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using ECS;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using UnityEngine;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(PrepareGlobalAssetBundleLoadingParametersSystem))]
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class FinalizeAssetBundleWearableLoadingSystem : FinalizeWearableLoadingSystemBase
    {
        private readonly AbgenConversionMetrics abgenConversionMetrics;

        public FinalizeAssetBundleWearableLoadingSystem(
            World world,
            IWearableStorage wearableStorage,
            IRealmData realmData,
            AbgenConversionMetrics abgenConversionMetrics
        ) : base(world, wearableStorage, realmData)
        {
            this.abgenConversionMetrics = abgenConversionMetrics;
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
            if (wearable.DTO is LocalSceneWearableDTO { RawGltfFallback: true } && TryFallBackToRawGltf(entity, ref promise, wearable, bodyShape, index))
                return;

            FinalizeAssetLoading(entity, ref promise, wearable, in bodyShape, index, result => result.ToWearableAsset(wearable));
        }

        /// <summary>
        ///     Mirrors the scene GLTF fallback: a bundle that failed to load, or loaded without a GameObject, is replaced
        ///     by a raw GLTF request for the same slot, so the wearable stays loading instead of failing.
        /// </summary>
        private bool TryFallBackToRawGltf(Entity entity, ref AssetBundlePromise promise, IWearable wearable, BodyShape bodyShape, int index)
        {
            Entity promiseEntity = promise.Entity;

            if (promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested
                || promiseEntity == Entity.Null
                || !World.IsAlive(promiseEntity)
                || !World.TryGet(promiseEntity, out StreamableLoadingResult<AssetBundleData> result))
                return false;

            AssetBundleData? bundle = result.Asset;
            string reason;

            if (!result.Succeeded)
                reason = result.Exception?.Message ?? "bundle failed to load";
            else if (bundle!.TryGetAsset(out GameObject _))
                return false;
            else
                reason = "bundle has no GameObject";

            IPartitionComponent partition = World.Get<IPartitionComponent>(promiseEntity);

            if (!WearablePolymorphicBehaviour.TryCreateRawGltfPromise(wearable, bodyShape, index, partition, World, out string? file))
                return false;

            // The bundle is cached and reference-counted; this consumer is leaving it for the raw file.
            if (result.Succeeded)
                bundle!.Dereference();

            if (abgenConversionMetrics.RawGltfFallbackReason == null)
                ReportHub.LogWarning(GetReportData(), $"{file}: asset bundle unavailable ({reason}), loading the raw GLTF instead");

            abgenConversionMetrics.OnFailed(file, $"{reason} — loaded as raw GLTF");

            promise.Consume(World);
            World.Destroy(entity);
            return true;
        }
    }
}
