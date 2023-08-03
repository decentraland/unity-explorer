using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Diagnostics.ReportsHandling;

namespace DCL.CharacterCamera
{
    /// <summary>
    ///     Must be updated after character's movement interpolation and rotation
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.MOTION)]
    public partial class CameraGroup { }
}
