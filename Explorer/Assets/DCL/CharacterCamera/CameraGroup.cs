using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;

namespace DCL.CharacterCamera
{
    /// <summary>
    ///     Must be updated after character's movement interpolation and rotation
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class CameraGroup { }
}
