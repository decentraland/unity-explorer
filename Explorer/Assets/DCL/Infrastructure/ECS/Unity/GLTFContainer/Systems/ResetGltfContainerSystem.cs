using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.Interaction.Utility;
using ECS.Abstract;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using System;
using System.Runtime.CompilerServices;
using Utility.Arch;

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
        private readonly IGltfContainerAssetsCache cache;
        private readonly IEntityCollidersSceneCache entityCollidersSceneCache;
        private readonly EntityEventBuffer<GltfContainerComponent> eventsBuffer;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;

        internal ResetGltfContainerSystem(World world,
            IGltfContainerAssetsCache cache,
            IEntityCollidersSceneCache entityCollidersSceneCache,
            EntityEventBuffer<GltfContainerComponent> eventsBuffer,
            IECSToCRDTWriter ecsToCRDTWriter) : base(world)
        {
            this.cache = cache;
            this.entityCollidersSceneCache = entityCollidersSceneCache;
            this.eventsBuffer = eventsBuffer;
            this.ecsToCRDTWriter = ecsToCRDTWriter;
        }

        protected override void Update(float t)
        {
            HandleComponentRemovalQuery(World);
            World.Remove<GltfContainerComponent>(in HandleComponentRemoval_QueryDescription);

            InvalidatePromiseQuery(World);
        }

        [Query]
        [None(typeof(PBGltfContainer))]
        private void HandleComponentRemoval(Entity entity, ref GltfContainerComponent component, CRDTEntity sdkEntity)
        {
            TryReleaseAsset(ref component);
            component.Promise.ForgetLoading(World);
            ecsToCRDTWriter.DeleteMessage<PBGltfContainerLoadingState>(sdkEntity);
            RemoveAnimationMarker(entity);
        }

        private void TryReleaseAsset(ref GltfContainerComponent component)
        {
            if (component.Promise.TryGetResult(World, out StreamableLoadingResult<GltfContainerAsset> result) && result.Succeeded)
            {
                component.ResetOriginalMaterials();
                cache.Dereference(component.Hash, result.Asset);
                entityCollidersSceneCache.Remove(result.Asset);
            }
        }

        /// <summary>
        ///     When asset is invalidated we must delete the previous Animation marker if it was added.<br/>
        ///     Leads to the structural change so it will invalidate arguments passed by `ref` and `in`
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RemoveAnimationMarker(Entity entity)
        {
            World.TryRemove<LegacyGltfAnimation>(entity);
        }

        [Query]
        private void InvalidatePromise(Entity entity, in PBGltfContainer sdkComponent, ref GltfContainerComponent component)
        {
            if (sdkComponent.IsDirty && !string.Equals(sdkComponent.Src, component.Name, StringComparison.OrdinalIgnoreCase))
            {
                TryReleaseAsset(ref component);

                // It will be a signal to create a new promise
                component.State = LoadingState.Unknown;
                component.Promise = AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.NULL;
                component.RootGameObject = null;

                if (World.Has<GltfNodeModifiers>(entity))
                    World.Add(entity, new GltfNodeModifiersCleanupIntention());

                eventsBuffer.Add(entity, component);
                RemoveAnimationMarker(entity);
            }
        }
    }
}
