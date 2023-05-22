using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.ComponentsPooling;
using ECS.Unity.Groups;
using ECS.Unity.PrimitiveColliders.Components;

namespace ECS.Unity.PrimitiveColliders.Systems
{
    /// <summary>
    ///     Releases the previous collider if its type was changed at runtime
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(ComponentInstantiationGroup))]
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
