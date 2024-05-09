using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using DCL.Interaction.Utility;
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
    [ThrottlingEnabled]
    public partial class ResetGltfContainerSystem : BaseUnityLoopSystem
    {
        private readonly IDereferencableCache<GltfContainerAsset, string> cache;
        private readonly IEntityCollidersSceneCache entityCollidersSceneCache;
        private readonly EntityEventBuffer<GltfContainerComponent> eventsBuffer;

        internal ResetGltfContainerSystem(World world, IDereferencableCache<GltfContainerAsset, string> cache, IEntityCollidersSceneCache entityCollidersSceneCache, EntityEventBuffer<GltfContainerComponent> eventsBuffer) : base(world)
        {
            this.cache = cache;
            this.entityCollidersSceneCache = entityCollidersSceneCache;
            this.eventsBuffer = eventsBuffer;
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
            {
                cache.Dereference(component.Source, result.Asset);
                entityCollidersSceneCache.Remove(result.Asset);
            }
        }

        [Query]
        private void InvalidatePromise(Entity entity, ref PBGltfContainer sdkComponent, ref GltfContainerComponent component)
        {
            if (sdkComponent.IsDirty && !string.Equals(sdkComponent.Src, component.Source, StringComparison.OrdinalIgnoreCase))
            {
                TryReleaseAsset(ref component);

                // It will be a signal to create a new promise
                component.State = LoadingState.Unknown;
                component.Promise = AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.NULL;
                eventsBuffer.Add(entity, component);
            }
        }
    }
}
