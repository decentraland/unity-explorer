using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using ECS.Groups;

namespace DCL.SDKComponents.ParticleSystem.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(ComponentInstantiationGroup))]
    [ThrottlingEnabled]
    public partial class ParticleSystemGroup { }
}
