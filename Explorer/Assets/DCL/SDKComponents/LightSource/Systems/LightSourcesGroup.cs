using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using ECS.Groups;

namespace DCL.SDKComponents.LightSource.Systems
{
    /// <summary>
    /// Group that should contain all systems handling light sources.
    /// </summary>
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(ComponentInstantiationGroup))]
    [ThrottlingEnabled]
    public partial class LightSourcesGroup { }
}
