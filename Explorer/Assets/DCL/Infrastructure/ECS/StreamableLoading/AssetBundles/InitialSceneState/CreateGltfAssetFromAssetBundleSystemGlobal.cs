using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Groups;
using ECS.SceneLifeCycle;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.AssetBundles.InitialSceneState;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;
using UnityEngine;

namespace ECS.Unity.GLTFContainer.Asset.Systems
{
    /// <summary>
    /// Creates assets from the static scene and adds them to the GltfContainerCache so they can be used by the scenes/lods
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(LoadGlobalSystemGroup))]
    [LogCategory(ReportCategory.GLTF_CONTAINER)]
    public partial class CreateGLTFAssetFromAssetBundleSystemGlobal : CreateGLTFAssetFromAssetBundleSystemBase
    {
        private IGltfContainerAssetsCache gltfContainerAssetsCache;

        internal CreateGLTFAssetFromAssetBundleSystemGlobal(World world, IPerformanceBudget instantiationFrameTimeBudget, IPerformanceBudget memoryBudget, IGltfContainerAssetsCache gltfContainerAssetsCache) : base(world, instantiationFrameTimeBudget, memoryBudget)
        {
            this.gltfContainerAssetsCache = gltfContainerAssetsCache;
        }

        protected override void Update(float t)
        {
            ConvertFromInitialSceneStateBundleQuery(World);
        }

        [Query]
        private void ConvertFromInitialSceneStateBundle(in Entity entity, ref GetGltfContainerAssetIntention assetIntention, ref StreamableLoadingResult<AssetBundleData> assetBundleResult, ref InitialSceneStateDescriptor staticSceneAssetBundle)
        {
            if (!HasBudget())
                return;

            if (assetIntention.CancellationTokenSource.IsCancellationRequested)

                // Don't care anymore, the entity will be deleted in the system that created this promise
                return;

            AssetBundleData assetBundleData = assetBundleResult.Asset!;

            GltfContainerAsset asset = CreateGltfObject(assetBundleData, assetBundleData.GetAsset<GameObject>(assetIntention.Hash), "static");

            gltfContainerAssetsCache.Dereference(assetIntention.Hash, asset);
            staticSceneAssetBundle.AddInstantiatedAsset(assetIntention.Hash, asset);

            //It was just a auxiliary entity. It can be destroyed
            World.Destroy(entity);
        }
    }

}
