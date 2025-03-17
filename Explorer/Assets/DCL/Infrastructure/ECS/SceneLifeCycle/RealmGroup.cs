using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;

namespace ECS.SceneLifeCycle
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [LogCategory(ReportCategory.SCENE_LOADING)]
    public partial class RealmGroup { }
}
