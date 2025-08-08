using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Components;
using UnityEngine;

namespace ECS.Unity.GLTFContainer.Asset.Systems
{
    /// <summary>
    ///     Creates <see cref="GltfContainerAsset" /> from the <see cref="StreamableLoadingResult{T}" />
    /// </summary>
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.GLTF_CONTAINER)]
    public partial class CreateGltfAssetFromAssetBundleSystemWorld : CreateGLTFAssetFromAssetBundleSystemBase
    {

        internal CreateGltfAssetFromAssetBundleSystemWorld(World world, IPerformanceBudget instantiationFrameTimeBudget, IPerformanceBudget memoryBudget) : base(world, instantiationFrameTimeBudget, memoryBudget) { }

        protected override void Update(float t)
        {
            ConvertFromAssetBundleQuery(World);
        }

        /// <summary>
        ///     Called on a separate entity with a promise creates a result with <see cref="GltfContainerAsset" />
        /// </summary>
        [Query]
        [None(typeof(StreamableLoadingResult<GltfContainerAsset>))]
        private void ConvertFromAssetBundle(in Entity entity, ref GetGltfContainerAssetIntention assetIntention, ref StreamableLoadingResult<AssetBundleData> assetBundleResult)
        {
            if (!HasBudget())
                return;

            if (assetIntention.CancellationTokenSource.IsCancellationRequested)

                // Don't care anymore, the entity will be deleted in the system that created this promise
                return;

            if (!assetBundleResult.Succeeded)
            {
                // Just propagate an exception, we can't do anything
                World.Add(entity, new StreamableLoadingResult<GltfContainerAsset>(GetReportCategory(), CreateException(assetBundleResult.Exception)));
                return;
            }

            AssetBundleData assetBundleData = assetBundleResult.Asset!;
            GltfContainerAsset result;

            // Create a new container root. It will be cached and pooled
            result = CreateGltfObject(assetBundleData, assetBundleData.GetMainAsset<GameObject>());

            World.Add(entity, new StreamableLoadingResult<GltfContainerAsset>(result));
        }


    }
}
