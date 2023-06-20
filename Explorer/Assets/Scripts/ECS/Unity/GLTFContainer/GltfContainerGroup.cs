using Arch.SystemGroups;
using ECS.Groups;
using ECS.StreamableLoading;

namespace ECS.Unity.GLTFContainer
{
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    [UpdateBefore(typeof(StreamableLoadingGroup))]
    public partial class GltfContainerGroup { }
}
