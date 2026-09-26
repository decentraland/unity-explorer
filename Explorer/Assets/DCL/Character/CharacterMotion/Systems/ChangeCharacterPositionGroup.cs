using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterCamera;
using DCL.Diagnostics;

namespace DCL.CharacterMotion.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(CameraGroup))]
    [LogCategory(ReportCategory.MOTION)]
    public partial class ChangeCharacterPositionGroup { }
}
