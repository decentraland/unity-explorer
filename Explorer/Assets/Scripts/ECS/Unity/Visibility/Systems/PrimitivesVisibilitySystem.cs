using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.Unity.Groups;
using ECS.Unity.PrimitiveRenderer.Components;

namespace ECS.Unity.Visibility.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.PRIMITIVE_MESHES)]
    public partial class PrimitivesVisibilitySystem : BaseUnityLoopSystem
    {
        public PrimitivesVisibilitySystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            UpdateVisibilityQuery(World);
            HandleComponentRemovalQuery(World);
        }

        [Query]
        private void UpdateVisibility(ref PBVisibilityComponent visibilityComponent,
            ref PBMeshRenderer meshRendererComponent, ref PrimitiveMeshRendererComponent primitiveMeshRendererComponent)
        {
            if (meshRendererComponent.IsDirty || visibilityComponent.IsDirty)
                primitiveMeshRendererComponent.MeshRenderer.enabled = visibilityComponent.GetVisible();
        }

        [Query]
        [None(typeof(PBVisibilityComponent))]
        private void HandleComponentRemoval(ref RemovedComponents removedComponents, ref PrimitiveMeshRendererComponent primitiveMeshRendererComponent)
        {
            if (removedComponents.Set.Remove(typeof(PBVisibilityComponent)))
                primitiveMeshRendererComponent.MeshRenderer.enabled = true;
        }
    }
}
