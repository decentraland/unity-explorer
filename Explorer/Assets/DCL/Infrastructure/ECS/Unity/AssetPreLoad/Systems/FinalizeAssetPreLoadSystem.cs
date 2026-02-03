using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CRDT;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.SDKComponents.MediaStream;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.AssetLoad.Cache;
using ECS.Unity.AssetLoad.Components;
using ECS.Unity.GLTFContainer;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.GLTFContainer.Systems;
using AudioPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AudioClips.AudioClipData, ECS.StreamableLoading.AudioClips.GetAudioClipIntention>;
using TexturePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.TextureData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace ECS.Unity.AssetLoad.Systems
{
    [UpdateInGroup(typeof(GltfContainerGroup))]
    [UpdateAfter(typeof(LoadGltfContainerSystem))]
    [ThrottlingEnabled]
    public partial class FinalizeAssetPreLoadSystem : BaseUnityLoopSystem
    {
        private readonly IPerformanceBudget capBudget;
        private readonly AssetPreLoadCache assetPreLoadCache;

        internal FinalizeAssetPreLoadSystem(World world,
            IPerformanceBudget capBudget,
            AssetPreLoadCache assetPreLoadCache)
            : base(world)
        {
            this.capBudget = capBudget;
            this.assetPreLoadCache = assetPreLoadCache;
        }

        protected override void Update(float t)
        {
            FinalizeGltfLoadingQuery(World);
            FinalizeAudioClipLoadingQuery(World);
            FinalizeTextureLoadingQuery(World);
            FinalizeVideoLoadingQuery(World);
        }

        [Query]
        [All(typeof(PBGltfContainer))]
        [None(typeof(CRDTEntity))]
        private void FinalizeGltfLoading(ref AssetPreLoadLoadingStateComponent assetPreLoadLoadingStateComponent, ref GltfContainerComponent component)
        {
            if (!capBudget.TrySpendBudget())
                return;

            if (component.State == LoadingState.Loading
                && !component.Promise.IsConsumed
                && component.Promise.TryConsume(World!, out StreamableLoadingResult<GltfContainerAsset> result))
            {
                if (result.Succeeded)
                    assetPreLoadCache.TryAdd(assetPreLoadLoadingStateComponent.AssetHash, result.Asset);

                MarkForUpdate(result.Succeeded ? LoadingState.Finished : LoadingState.FinishedWithError, ref assetPreLoadLoadingStateComponent);
            }
        }

        [Query]
        [None(typeof(CRDTEntity))]
        private void FinalizeAudioClipLoading(ref AssetPreLoadLoadingStateComponent assetPreLoadLoadingStateComponent, ref AudioPromise audioPromise)
        {
            if (audioPromise.IsConsumed
                || !capBudget.TrySpendBudget())
                return;

            if (!audioPromise.TryConsume(World!, out var promiseResult))
                return;

            if (promiseResult.Succeeded)
                assetPreLoadCache.TryAdd(assetPreLoadLoadingStateComponent.AssetHash, promiseResult.Asset);

            MarkForUpdate(promiseResult.Succeeded ? LoadingState.Finished : LoadingState.FinishedWithError, ref assetPreLoadLoadingStateComponent);
        }

        [Query]
        [None(typeof(CRDTEntity))]
        private void FinalizeTextureLoading(ref AssetPreLoadLoadingStateComponent assetPreLoadLoadingStateComponent, ref TexturePromise texturePromise)
        {
            if (texturePromise.IsConsumed
                || !capBudget.TrySpendBudget())
                return;

            if (!texturePromise.TryConsume(World!, out var promiseResult))
                return;

            if (promiseResult.Succeeded)
                assetPreLoadCache.TryAdd(assetPreLoadLoadingStateComponent.AssetHash, promiseResult.Asset);

            MarkForUpdate(promiseResult.Succeeded ? LoadingState.Finished : LoadingState.FinishedWithError, ref assetPreLoadLoadingStateComponent);
        }

        [Query]
        [All(typeof(PBVideoPlayer))]
        [None(typeof(CRDTEntity))]
        private void FinalizeVideoLoading(ref AssetPreLoadLoadingStateComponent assetPreLoadLoadingStateComponent, ref MediaPlayerComponent mediaPlayerComponent)
        {
            //UpdateMediaPlayerSystem already tried to consume the promise, so we just need to check if it was consumed or not
            if (mediaPlayerComponent.OpenMediaPromise?.IsConsumed == false
                || !capBudget.TrySpendBudget())
                return;

            if (!mediaPlayerComponent.HasFailed)
                assetPreLoadCache.TryAdd(mediaPlayerComponent.MediaAddress.ToString(), mediaPlayerComponent);

            MarkForUpdate(mediaPlayerComponent.HasFailed ? LoadingState.FinishedWithError : LoadingState.Finished, ref assetPreLoadLoadingStateComponent);
        }

        private void MarkForUpdate(LoadingState loadingState, ref AssetPreLoadLoadingStateComponent loadingStateComponent)
        {
            loadingStateComponent.LoadingState = loadingState;
            loadingStateComponent.IsDirty = true;
        }
    }
}
