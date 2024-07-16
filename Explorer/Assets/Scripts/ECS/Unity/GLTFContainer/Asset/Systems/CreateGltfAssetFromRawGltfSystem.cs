using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Metadata;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Abstract;
using ECS.StreamableLoading;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.GLTF;
using ECS.Unity.GLTFContainer.Asset.Components;
using System;
using UnityEngine;

namespace ECS.Unity.GLTFContainer.Asset.Systems
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.GLTF_CONTAINER)]
    public partial class CreateGltfAssetFromRawGltfSystem : BaseUnityLoopSystem
    {
        private readonly IPerformanceBudget instantiationFrameTimeBudget;
        private readonly IPerformanceBudget memoryBudget;

        internal CreateGltfAssetFromRawGltfSystem(World world, IPerformanceBudget instantiationFrameTimeBudget, IPerformanceBudget memoryBudget) : base(world)
        {
            this.instantiationFrameTimeBudget = instantiationFrameTimeBudget;
            this.memoryBudget = memoryBudget;
        }

        protected override void Update(float t)
        {
            PutStreamableLoadingResultQuery(World);
        }

        [Query]
        [None(typeof(StreamableLoadingResult<GltfContainerAsset>))]
        private void PutStreamableLoadingResult(in Entity entity,
            ref GetGltfContainerAssetIntention assetIntention,
            //ref StreamableLoadingResult<AssetBundleData> assetBundleResult
            ref GetGLTFIntention gltfIntention
            )
        {
            // GltfContainerAsset result = CreateGltfObject(gltfIntention.Name);
            Debug.Log($"gltfIntention: {gltfIntention.Name}");
            World.Add(entity, new StreamableLoadingResult<GltfContainerAsset>());
        }

        private static GltfContainerAsset CreateGltfObject(string assetName)
        {
            var container = new GameObject(assetName);

            // Let the upper layer decide what to do with the root
            container.SetActive(false);
            Transform containerTransform = container.transform;

            //var result = GltfContainerAsset.Create(container, new AssetBundleData());

            return default;
        }
    }
}
