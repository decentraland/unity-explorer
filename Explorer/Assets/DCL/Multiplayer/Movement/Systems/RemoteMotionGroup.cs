using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape;

namespace DCL.Multiplayer.Movement.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(AvatarGroup))]
    public partial class RemoteMotionGroup
    {
    }
}
