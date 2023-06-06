using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Unity.Groups;
using ECS.Unity.PrimitiveRenderer.Components;
using ECS.Unity.PrimitiveRenderer.Systems;

namespace ECS.Unity.Visibility.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [UpdateAfter(typeof(InstantiatePrimitiveRenderingSystem))]
    public partial class VisibilitySystem : BaseUnityLoopSystem
    {
        public VisibilitySystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            UpdateVisibilityQuery(World);
            HandleComponentRemovalQuery(World);
        }

        [Query]
        private void UpdateVisibility(ref PBVisibilityComponent visibilityComponent,
            ref PBMeshRenderer meshRendererComponent, ref PrimitiveMeshRendererComponent primitiveMeshRendererComponent)
        {
            if (!meshRendererComponent.IsDirty && !visibilityComponent.IsDirty)
                return;

            primitiveMeshRendererComponent.MeshRenderer.enabled = visibilityComponent.Visible;
        }

        [Query]
        [None(typeof(PBVisibilityComponent))]
        private void HandleComponentRemoval(ref RemovedComponents removedComponents, ref PrimitiveMeshRendererComponent primitiveMeshRendererComponent)
        {
            if (removedComponents.RemovedComponentsSet.Remove(typeof(PBVisibilityComponent)))
                primitiveMeshRendererComponent.MeshRenderer.enabled = true;
        }
    }
}
