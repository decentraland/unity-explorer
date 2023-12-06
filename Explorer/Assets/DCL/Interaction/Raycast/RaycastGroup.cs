using Arch.SystemGroups;
using ECS.Groups;
using ECS.Unity.Groups;
using ECS.Unity.Transforms.Systems;

namespace DCL.Interaction.Raycast
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(ComponentInstantiationGroup))] // after all components are instantiated
    [UpdateAfter(typeof(InstantiateTransformSystem))] // after all transforms are instantiated
    public partial class RaycastGroup { }
}
