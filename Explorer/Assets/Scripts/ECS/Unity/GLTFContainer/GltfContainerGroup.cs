using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.StreamableLoading;

namespace ECS.Unity.GLTFContainer
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(StreamableLoadingGroup))]
    public partial class GltfContainerGroup { }
}
