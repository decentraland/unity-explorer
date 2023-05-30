using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;

namespace ECS.Groups
{
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    public partial class CleanUpGroup { }
}
