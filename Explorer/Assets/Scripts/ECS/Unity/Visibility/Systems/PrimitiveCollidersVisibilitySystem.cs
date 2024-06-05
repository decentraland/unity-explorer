using Arch.Core;
using Arch.SystemGroups;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Groups;
using ECS.Unity.PrimitiveColliders.Components;

namespace ECS.Unity.Visibility.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.PRIMITIVE_COLLIDERS)]
    public partial class PrimitiveCollidersVisibilitySystem : VisibilitySystemBase<PrimitiveColliderComponent>
    {
        public PrimitiveCollidersVisibilitySystem(World world, EntityEventBuffer<PrimitiveColliderComponent> eventsBuffer) : base(world, eventsBuffer)
        {
        }

        protected override void UpdateVisibilityInternal(ref PrimitiveColliderComponent component, bool visible)
        {
            component.IsVisible = visible;
            component.Collider.enabled = visible;
        }
    }
}
