using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;

namespace DCL.CharacterMotion.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.MOTION)]
    public partial class ChangeCharacterPositionGroup { }
}
