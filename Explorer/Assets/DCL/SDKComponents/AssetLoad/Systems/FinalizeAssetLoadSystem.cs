using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.SDKComponents.AssetLoad.Components;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.GLTFContainer.Systems;
using ECS.Unity.Visibility.Systems;
using SceneRunner.Scene;
using System;

namespace DCL.SDKComponents.AssetLoad.Systems
{
    [UpdateInGroup(typeof(GltfContainerGroup))]
    [UpdateAfter(typeof(LoadGltfContainerSystem))]
    public partial class FinalizeAssetLoadSystem : BaseUnityLoopSystem
    {
        private readonly IPerformanceBudget capBudget;
        private readonly IGltfContainerAssetsCache gltfCache;
        private readonly AssetLoadUtils assetLoadUtils;

        internal FinalizeAssetLoadSystem(World world,
            IPerformanceBudget capBudget,
            IGltfContainerAssetsCache gltfCache,
            AssetLoadUtils assetLoadUtils)
            : base(world)
        {
            this.capBudget = capBudget;
            this.gltfCache = gltfCache;
            this.assetLoadUtils = assetLoadUtils;
        }


        protected override void Update(float t)
        {
            if (!capBudget.TrySpendBudget())
                return;

            FinalizeGltfLoadingQuery(World);
        }

        [Query]
        [All(typeof(PBGltfContainer))]
        [None(typeof(CRDTEntity))]
        private void FinalizeGltfLoading(ref AssetLoadChildComponent assetLoadChildComponent, ref GltfContainerComponent component)
        {
            if (component.State == LoadingState.Loading
                && component.Promise.TryConsume(World!, out StreamableLoadingResult<GltfContainerAsset> result))
            {
                if (result.Succeeded)
                {
                    gltfCache.Dereference(component.Hash, result.Asset);
                    assetLoadUtils.AppendAssetLoadingMessage(assetLoadChildComponent.Parent, LoadingState.Finished, component.Name);
                }
                else
                    assetLoadUtils.AppendAssetLoadingMessage(assetLoadChildComponent.Parent, LoadingState.FinishedWithError, component.Name);
            }
        }
    }
}
