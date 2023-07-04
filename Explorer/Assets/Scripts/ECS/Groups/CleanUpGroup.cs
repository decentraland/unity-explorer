using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;

namespace ECS.Groups
{
    [UpdateInGroup(typeof(SyncedPostRenderingSystemGroup))]
    [ThrottlingEnabled]
    public partial class CleanUpGroup { }
}
