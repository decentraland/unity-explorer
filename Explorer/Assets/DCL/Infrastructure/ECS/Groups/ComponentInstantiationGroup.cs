using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;

namespace ECS.Groups
{
    /// <summary>
    ///     Denotes the group that instantiates specific components right after the entity transform is handled
    /// </summary>
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [ThrottlingEnabled]
    public partial class ComponentInstantiationGroup { }
}
