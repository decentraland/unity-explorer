using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.StreamableLoading;

namespace ECS.Unity.Materials
{
    /// <summary>
    ///     Material systems create intentions to load textures so they should be executed before
    ///     Streamable Loading Group
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(StreamableLoadingGroup))]
    public partial class MaterialLoadingGroup { }
}
