using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
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
    public partial class FinalizeAssetLoadSystem : BaseUnityLoopSystem
    {
        private readonly IPerformanceBudget capBudget;
        private readonly AssetLoadCache assetLoadCache;
        private readonly AssetLoadUtils assetLoadUtils;

        internal FinalizeAssetLoadSystem(World world,
            IPerformanceBudget capBudget,
            AssetLoadCache assetLoadCache,
            AssetLoadUtils assetLoadUtils)
            : base(world)
        {
            this.capBudget = capBudget;
            this.assetLoadCache = assetLoadCache;
            this.assetLoadUtils = assetLoadUtils;
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
        private void FinalizeGltfLoading(in Entity entity, ref AssetLoadChildComponent assetLoadChildComponent, ref GltfContainerComponent component)
        {
            if (!capBudget.TrySpendBudget())
                return;

            if (component.State == LoadingState.Loading
                && component.Promise.TryConsume(World!, out StreamableLoadingResult<GltfContainerAsset> result))
            {
                if (result.Succeeded)
                {
                    assetLoadCache.TryAdd(assetLoadChildComponent.AssetHash, result.Asset);
                    assetLoadUtils.AppendAssetLoadingMessage(assetLoadChildComponent.Parent, LoadingState.Finished, assetLoadChildComponent.AssetPath);
                }
                else
                    assetLoadUtils.AppendAssetLoadingMessage(assetLoadChildComponent.Parent, LoadingState.FinishedWithError, assetLoadChildComponent.AssetPath);

                World.Destroy(entity);
            }
        }

        [Query]
        [None(typeof(CRDTEntity))]
        private void FinalizeAudioClipLoading(in Entity entity, ref AssetLoadChildComponent assetLoadChildComponent, ref AudioPromise audioPromise)
        {
            if (audioPromise.IsConsumed
                || !capBudget.TrySpendBudget())
                return;

            if (!audioPromise.TryConsume(World!, out var promiseResult))
                return;

            if (promiseResult.Succeeded)
                assetLoadCache.TryAdd(assetLoadChildComponent.AssetHash, promiseResult.Asset);

            assetLoadUtils.AppendAssetLoadingMessage(assetLoadChildComponent.Parent, promiseResult.Succeeded ? LoadingState.Finished : LoadingState.FinishedWithError, assetLoadChildComponent.AssetPath);

            World.Destroy(entity);
        }

        [Query]
        [None(typeof(CRDTEntity))]
        private void FinalizeTextureLoading(in Entity entity, ref AssetLoadChildComponent assetLoadChildComponent, ref TexturePromise texturePromise)
        {
            if (texturePromise.IsConsumed
                || !capBudget.TrySpendBudget())
                return;

            if (!texturePromise.TryConsume(World!, out var promiseResult))
                return;

            if (promiseResult.Succeeded)
                assetLoadCache.TryAdd(assetLoadChildComponent.AssetHash, promiseResult.Asset);

            assetLoadUtils.AppendAssetLoadingMessage(assetLoadChildComponent.Parent, promiseResult.Succeeded ? LoadingState.Finished : LoadingState.FinishedWithError, assetLoadChildComponent.AssetPath);

            World.Destroy(entity);
        }

        [Query]
        [All(typeof(PBVideoPlayer))]
        [None(typeof(CRDTEntity))]
        private void FinalizeVideoLoading(in Entity entity, ref AssetLoadChildComponent assetLoadChildComponent, ref MediaPlayerComponent mediaPlayerComponent)
        {
            //UpdateMediaPlayerSystem already tried to consume the promise, so we just need to check if it was consumed or not
            if (mediaPlayerComponent.OpenMediaPromise?.IsConsumed == false
                || !capBudget.TrySpendBudget())
                return;

            if (!mediaPlayerComponent.HasFailed)
                assetLoadCache.TryAdd(mediaPlayerComponent.MediaAddress.ToString(), mediaPlayerComponent);

            assetLoadUtils.AppendAssetLoadingMessage(assetLoadChildComponent.Parent, mediaPlayerComponent.HasFailed ? LoadingState.FinishedWithError : LoadingState.Finished, assetLoadChildComponent.AssetPath);

            World.Destroy(entity);
        }
    }
}
