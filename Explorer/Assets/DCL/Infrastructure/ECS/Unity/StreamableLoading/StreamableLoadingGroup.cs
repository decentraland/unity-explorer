using Arch.SystemGroups;
using DCL.Diagnostics;
using ECS.Groups;

namespace ECS.StreamableLoading
{
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    [LogCategory(ReportCategory.STREAMABLE_LOADING)]
    public partial class StreamableLoadingGroup { }
}
