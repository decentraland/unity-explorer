using Arch.SystemGroups;
using ECS.Groups;
using ECS.StreamableLoading;

namespace ECS.Unity.Materials
{
    /// <summary>
    ///     Material systems create intentions to load textures so they should be executed before
    ///     Streamable Loading Group
    /// </summary>
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    [UpdateBefore(typeof(StreamableLoadingGroup))]
    public partial class MaterialLoadingGroup { }
}
