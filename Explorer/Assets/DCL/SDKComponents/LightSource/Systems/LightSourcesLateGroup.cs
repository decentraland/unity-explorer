using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using ECS.Groups;

namespace DCL.SDKComponents.LightSource.Systems
{
    /// <summary>
    /// Same as <see cref="LightSourcesGroup"/>, but runs after it.
    /// </summary>
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(LightSourcesGroup))]
    [ThrottlingEnabled]
    public partial class LightSourcesLateGroup { }
}
