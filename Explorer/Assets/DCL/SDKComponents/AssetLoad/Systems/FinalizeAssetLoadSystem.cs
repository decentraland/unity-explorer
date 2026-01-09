using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
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
using AudioPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AudioClips.AudioClipData, ECS.StreamableLoading.AudioClips.GetAudioClipIntention>;
using TexturePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.TextureData, ECS.StreamableLoading.Textures.GetTextureIntention>;

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
            FinalizeGltfLoadingQuery(World);
            FinalizeAudioClipLoadingQuery(World);
        }

        [Query]
        [All(typeof(PBGltfContainer))]
        [None(typeof(CRDTEntity))]
        private void FinalizeGltfLoading(ref AssetLoadChildComponent assetLoadChildComponent, ref GltfContainerComponent component)
        {
            if (!capBudget.TrySpendBudget())
                return;

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

        [Query]
        [None(typeof(CRDTEntity))]
        private void FinalizeAudioClipLoading(ref AssetLoadChildComponent assetLoadChildComponent, ref AudioPromise audioPromise)
        {
            if (audioPromise.IsConsumed
                || !capBudget.TrySpendBudget())
                return;

            if (!audioPromise.TryConsume(World!, out var promiseResult))
                return;

            assetLoadUtils.AppendAssetLoadingMessage(assetLoadChildComponent.Parent, promiseResult.Succeeded ? LoadingState.Finished : LoadingState.FinishedWithError, audioPromise.LoadingIntention.CommonArguments.URL);
        }

        [Query]
        [None(typeof(CRDTEntity))]
        private void FinalizeTextureLoading(ref AssetLoadChildComponent assetLoadChildComponent, ref TexturePromise texturePromise)
        {
            if (texturePromise.IsConsumed
                || !capBudget.TrySpendBudget())
                return;

            if (!texturePromise.TryConsume(World!, out var promiseResult))
                return;

            assetLoadUtils.AppendAssetLoadingMessage(assetLoadChildComponent.Parent, promiseResult.Succeeded ? LoadingState.Finished : LoadingState.FinishedWithError, texturePromise.LoadingIntention.CommonArguments.URL);
        }
    }
}
