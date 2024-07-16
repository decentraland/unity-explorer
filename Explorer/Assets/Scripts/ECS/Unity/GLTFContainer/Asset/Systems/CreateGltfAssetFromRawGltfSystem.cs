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
            ref StreamableLoadingResult<GLTFData> gltfData)
        {
            if (assetIntention.CancellationTokenSource.IsCancellationRequested)
                return;

            World.Add(entity, new StreamableLoadingResult<GltfContainerAsset>(CreateGltfObject(ref gltfData)));
        }

        private static GltfContainerAsset CreateGltfObject(ref StreamableLoadingResult<GLTFData> gltfData)
        {
            // TODO: Create container GameObject, containerTransform, instantiate GLTF GameObject and
            // populate GltfContainerAsset; Check 'CreateGltfAssetFromAssetBundleSystem.CreateGltfObject()'...

            // var result = GltfContainerAsset.Create(container, gltfData);

            // ...

            return default;
        }
    }
}
