using Arch.SystemGroups;

namespace ECS.Groups
{
    [UpdateInGroup(typeof(SyncedPostRenderingSystemGroup))]
    public partial class CleanUpGroup { }
}
