using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;

namespace ECS.Groups
{
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [ThrottlingEnabled]
    public partial class CleanUpGroup { }
}
