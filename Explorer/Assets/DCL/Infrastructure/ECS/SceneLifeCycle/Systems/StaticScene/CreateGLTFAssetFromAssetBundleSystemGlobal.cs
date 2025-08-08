using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using ECS.SceneLifeCycle;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;
using UnityEngine;

namespace ECS.Unity.GLTFContainer.Asset.Systems
{
    /// <summary>
    /// Creates assets from the static scene and adds them to the GltfContainerCache so they can be used by the scenes/lods
    /// </summary>
    [UpdateInGroup(typeof(RealmGroup))]
    [LogCategory(ReportCategory.GLTF_CONTAINER)]
    public partial class CreateGLTFAssetFromAssetBundleSystemGlobal : CreateGLTFAssetFromAssetBundleSystemBase
    {
        private GltfContainerAssetsCache gltfContainerAssetsCache;

        internal CreateGLTFAssetFromAssetBundleSystemGlobal(World world, IPerformanceBudget instantiationFrameTimeBudget, IPerformanceBudget memoryBudget, GltfContainerAssetsCache gltfContainerAssetsCache) : base(world, instantiationFrameTimeBudget, memoryBudget)
        {
            this.gltfContainerAssetsCache = gltfContainerAssetsCache;
        }

        protected override void Update(float t)
        {
            ConvertFromStaticAssetBundleQuery(World);
        }

        /// <summary>
        ///     Called on a separate entity with a promise creates a result with <see cref="GltfContainerAsset" />
        /// </summary>
        [Query]
        private void ConvertFromStaticAssetBundle(in Entity entity, ref GetGltfContainerAssetIntention assetIntention, ref StreamableLoadingResult<AssetBundleData> assetBundleResult)
        {
            if (!HasBudget())
                return;

            if (assetIntention.CancellationTokenSource.IsCancellationRequested)

                // Don't care anymore, the entity will be deleted in the system that created this promise
                return;

            AssetBundleData assetBundleData = assetBundleResult.Asset!;

            gltfContainerAssetsCache.Dereference(assetIntention.Hash, CreateGltfObject(assetBundleData, assetBundleData.GetAsset<GameObject>(assetIntention.Hash), "static"));

            //It was just a auxiliary entity. It can be destroyed
            World.Destroy(entity);
        }
    }
}
