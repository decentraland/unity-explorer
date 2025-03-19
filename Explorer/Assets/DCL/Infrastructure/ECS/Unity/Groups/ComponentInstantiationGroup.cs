using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using ECS.Groups;
using ECS.Unity.Transforms.Systems;

namespace ECS.Unity.Groups
{
    /// <summary>
    ///     Denotes the group that instantiates specific components right after the entity transform is handled
    /// </summary>
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(UpdateTransformSystem))]
    [ThrottlingEnabled]
    public partial class ComponentInstantiationGroup { }
}
