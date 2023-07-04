using Arch.SystemGroups;
using ECS.Groups;

namespace ECS.StreamableLoading
{
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    public partial class StreamableLoadingGroup { }
}
