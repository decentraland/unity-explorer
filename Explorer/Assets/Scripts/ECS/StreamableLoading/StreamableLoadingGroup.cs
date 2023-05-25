using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;

namespace ECS.StreamableLoading
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class StreamableLoadingGroup { }
}
