using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.ComponentsPooling;
using ECS.Unity.Groups;
using ECS.Unity.PrimitiveRenderer.Components;
using UnityEngine;

namespace ECS.Unity.PrimitiveRenderer.Systems
{
    /// <summary>
    ///     Releases the previous collider if its type was changed at runtime
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(ComponentInstantiationGroup))]
    public partial class ReleaseOutdatedRenderingSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPoolsRegistry poolsRegistry;

        internal ReleaseOutdatedRenderingSystem(World world, IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            this.poolsRegistry = poolsRegistry;
        }

        protected override void Update(float t)
        {
            ValidateRenderingQuery(World);
        }

        [Query]
        [All(typeof(PBMeshRenderer), typeof(PrimitiveMeshComponent))]
        private void ValidateRendering(ref PBMeshRenderer meshRenderer, ref PrimitiveMeshComponent component)
        {
            if (meshRenderer.IsDirty && meshRenderer.MeshCase != component.SDKType)
            {
                if (poolsRegistry.TryGetPool(typeof(Mesh), out IComponentPool componentPool))
                    componentPool.Release(component.PoolableComponent);

                // it will be a signal to instantiate a new collider
                component.Mesh = null;
            }
        }
    }
}
