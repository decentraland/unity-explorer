using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Unity.Systems;

namespace ECS.Unity.Groups
{
    /// <summary>
    ///     Denotes the group that instantiates specific components right after the entity transform is handled
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(InstantiateTransformSystem))]
    public partial class ComponentInstantiationGroup { }
}
