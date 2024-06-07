using Arch.Core;
using Arch.SystemGroups;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Groups;
using ECS.Unity.PrimitiveRenderer.Components;

namespace ECS.Unity.Visibility.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.PRIMITIVE_MESHES)]
    public partial class PrimitivesVisibilitySystem : VisibilitySystemBase<PrimitiveMeshRendererComponent>
    {
        public PrimitivesVisibilitySystem(World world, EntityEventBuffer<PrimitiveMeshRendererComponent> changedMeshes)
            : base(world, changedMeshes)
        {
        }

        protected override void UpdateVisibilityInternal(in PrimitiveMeshRendererComponent component, bool visible)
        {
            component.MeshRenderer.enabled = visible;
        }
    }
}
