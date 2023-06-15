using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using System;

namespace ECS.Unity.GLTFContainer.Systems
{
    /// <summary>
    ///     Returns Gltf Container to the pool if the component it is associated with is altered
    /// </summary>
    [UpdateInGroup(typeof(GltfContainerGroup))]
    [UpdateBefore(typeof(LoadGltfContainerSystem))]
    public partial class ResetGltfContainerSystem : BaseUnityLoopSystem
    {
        private readonly IStreamableCache<GltfContainerAsset, string> cache;

        internal ResetGltfContainerSystem(World world, IStreamableCache<GltfContainerAsset, string> cache) : base(world)
        {
            this.cache = cache;
        }

        protected override void Update(float t)
        {
            HandleComponentRemovalQuery(World);
            World.Remove<GltfContainerComponent>(in HandleComponentRemoval_QueryDescription);

            InvalidatePromiseQuery(World);
        }

        [Query]
        [None(typeof(PBGltfContainer))]
        private void HandleComponentRemoval(ref GltfContainerComponent component)
        {
            TryReleaseAsset(ref component);
            component.Promise.ForgetLoading(World);
        }

        private void TryReleaseAsset(ref GltfContainerComponent component)
        {
            if (component.Promise.TryGetResult(World, out StreamableLoadingResult<GltfContainerAsset> result) && result.Succeeded)
                cache.Dereference(component.Source, result.Asset);
        }

        [Query]
        private void InvalidatePromise(ref PBGltfContainer sdkComponent, ref GltfContainerComponent component)
        {
            if (sdkComponent.IsDirty && !string.Equals(sdkComponent.Src, component.Source, StringComparison.OrdinalIgnoreCase))
            {
                TryReleaseAsset(ref component);

                // It will be a signal to create a new promise
                component.State.Set(LoadingState.Unknown);
                component.Promise = AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.NULL;
            }
        }
    }
}
