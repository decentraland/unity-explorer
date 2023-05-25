using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;

namespace ECS.StreamableLoading
{
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    public partial class StreamableLoadingGroup { }
}
