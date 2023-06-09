﻿using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using Diagnostics.ReportsHandling;
using ECS.Abstract;
using ECS.ComponentsPooling;
using ECS.Groups;
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

        internal ReleaseOutdatedColliderSystem(World world, IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            this.poolsRegistry = poolsRegistry;
        }

        protected override void Update(float t)
        {
            ValidateColliderQuery(World);
            HandleComponentRemovalQuery(World);

            // Batch remove
            World.Remove<PrimitiveColliderComponent>(in HandleComponentRemoval_QueryDescription);
        }

        [Query]
        [None(typeof(PBMeshCollider))]
        private void HandleComponentRemoval(ref PrimitiveColliderComponent component)
        {
            if (poolsRegistry.TryGetPool(component.ColliderType, out IComponentPool componentPool))
                componentPool.Release(component.Collider);
        }

        [Query]
        [All(typeof(PBMeshCollider), typeof(PrimitiveColliderComponent))]
        private void ValidateCollider(ref PBMeshCollider meshCollider, ref PrimitiveColliderComponent component)
        {
            if (meshCollider.IsDirty && meshCollider.MeshCase != component.SDKType)
            {
                if (poolsRegistry.TryGetPool(component.ColliderType, out IComponentPool componentPool))
                    componentPool.Release(component.Collider);

                // it will be a signal to instantiate a new collider
                component.Collider = null;
            }
        }
    }
}
