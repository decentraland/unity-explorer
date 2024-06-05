using Arch.Core;
using Arch.SystemGroups;
using DCL.SDKComponents.NFTShape.Component;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Groups;
using ECS.Unity.Visibility.Systems;

namespace DCL.SDKComponents.NFTShape.System
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(ComponentInstantiationGroup))]
    public partial class VisibilityNftShapeSystem : VisibilitySystemBase<NftShapeRendererComponent>
    {
        public VisibilityNftShapeSystem(World world, EntityEventBuffer<NftShapeRendererComponent> eventsBuffer) : base(world, eventsBuffer)
        {
        }

        protected override void UpdateVisibilityInternal(ref NftShapeRendererComponent component, bool visible)
        {
            component.ApplyVisibility(visible);
        }
    }
}
