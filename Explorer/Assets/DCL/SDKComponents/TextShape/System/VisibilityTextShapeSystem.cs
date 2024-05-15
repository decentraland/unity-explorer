using Arch.Core;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.SDKComponents.TextShape.Component;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Groups;
using ECS.Unity.Visibility.Systems;

namespace DCL.SDKComponents.TextShape.System
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.PRIMITIVE_MESHES)]
    public partial class VisibilityTextShapeSystem : VisibilitySystemBase<TextShapeComponent>
    {
        public VisibilityTextShapeSystem(World world, EntityEventBuffer<TextShapeComponent> changedTextMeshes) : base(world, changedTextMeshes) { }

        protected override void UpdateVisibilityInternal(in TextShapeComponent component, bool visible)
        {
            component.TextMeshPro.enabled = visible;
        }
    }
}
