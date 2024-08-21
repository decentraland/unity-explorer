using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Interaction.Utility;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.Unity.Groups;
using ECS.Unity.PrimitiveColliders.Components;

namespace ECS.Unity.PrimitiveColliders.Systems
{
    /// <summary>
    ///     Releases the previous collider if its type was changed at runtime or the SDK component is removed
    /// </summary>
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateBefore(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.PRIMITIVE_COLLIDERS)]
    [ThrottlingEnabled]
    public partial class ReleaseOutdatedColliderSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPoolsRegistry poolsRegistry;
        private readonly IEntityCollidersSceneCache entityCollidersSceneCache;

        internal ReleaseOutdatedColliderSystem(World world, IComponentPoolsRegistry poolsRegistry,
            IEntityCollidersSceneCache entityCollidersSceneCache) : base(world)
        {
            this.poolsRegistry = poolsRegistry;
            this.entityCollidersSceneCache = entityCollidersSceneCache;
        }

        protected override void Update(float t)
        {
            ValidateColliderQuery(World);
            HandleComponentRemovalQuery(World);
            RemoveFromCacheQuery(World);

            // Batch remove
            World.Remove<PrimitiveColliderComponent>(in HandleComponentRemoval_QueryDescription);
        }

        [Query]
        [None(typeof(PBMeshCollider))]
        private void HandleComponentRemoval(ref PrimitiveColliderComponent component)
        {
            Release(ref component);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void RemoveFromCache(ref PrimitiveColliderComponent colliderComponent)
        {
            entityCollidersSceneCache.Remove(colliderComponent.Collider);
        }

        [Query]
        [All(typeof(PBMeshCollider), typeof(PrimitiveColliderComponent))]
        private void ValidateCollider(ref PBMeshCollider meshCollider, ref PrimitiveColliderComponent component)
        {
            if (meshCollider.IsDirty && meshCollider.MeshCase != component.SDKType)
            {
                Release(ref component);
                component.Invalidate(); // it will be a signal to instantiate a new collider
            }
        }

        private void Release(ref PrimitiveColliderComponent component)
        {
            entityCollidersSceneCache.Remove(component.Collider);

            if (poolsRegistry.TryGetPool(component.ColliderType, out IComponentPool componentPool))
                componentPool.Release(component.Collider);
        }
    }
}
