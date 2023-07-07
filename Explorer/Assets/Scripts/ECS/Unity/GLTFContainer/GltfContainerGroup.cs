using Arch.SystemGroups;
using Diagnostics.ReportsHandling;
using ECS.Groups;
using ECS.StreamableLoading;

namespace ECS.Unity.GLTFContainer
{
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    [UpdateBefore(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.GLTF_CONTAINER)]
    public partial class GltfContainerGroup { }
}
