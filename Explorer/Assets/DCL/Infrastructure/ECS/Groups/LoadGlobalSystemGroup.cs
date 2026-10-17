using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;

namespace ECS.Groups
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LoadGlobalSystemGroup { }
}
