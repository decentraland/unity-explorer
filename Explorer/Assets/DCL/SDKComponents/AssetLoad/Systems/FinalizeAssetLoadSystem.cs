using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.GLTFContainer.Systems;
using ECS.Unity.Visibility.Systems;
using System;

namespace DCL.SDKComponents.AssetLoad.Systems
{
    [UpdateInGroup(typeof(GltfContainerGroup))]
    [UpdateAfter(typeof(LoadGltfContainerSystem))]
    public partial class FinalizeAssetLoadSystem : BaseUnityLoopSystem
    {
        private readonly IPerformanceBudget capBudget;
        private readonly IGltfContainerAssetsCache gltfCache;

        internal FinalizeAssetLoadSystem(World world,
            IPerformanceBudget capBudget,
            IGltfContainerAssetsCache gltfCache) : base(world)
        {
            this.capBudget = capBudget;
            this.gltfCache = gltfCache;
        }


        protected override void Update(float t)
        {
            if (!capBudget.TrySpendBudget())
                return;

            FinalizeGltfLoadingQuery(World);
        }

        [Query]
        [All(typeof(PBGltfContainer))]
        private void FinalizeGltfLoading(in Entity entity, ref GltfContainerComponent component)
        {
            if (component.State == LoadingState.Loading
                && component.Promise.TryConsume(World!, out StreamableLoadingResult<GltfContainerAsset> result)
                && result.Succeeded)
            {
                gltfCache.Dereference(component.Hash, result.Asset);
            }
        }
    }
}
