using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;

namespace MVC
{
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    public partial class ExampleControllerSystem : ControllerECSBridgeSystem
    {
        internal ExampleControllerSystem(World world) : base(world) { }
    }
}
