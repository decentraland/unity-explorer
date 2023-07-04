using Arch.SystemGroups;
using Diagnostics.ReportsHandling;
using ECS.Groups;

namespace ECS.StreamableLoading
{
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    [LogCategory(ReportCategory.STREAMABLE_LOADING)]
    public partial class StreamableLoadingGroup { }
}
