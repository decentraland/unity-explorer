using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Diagnostics.ReportsHandling;

namespace ECS.SceneLifeCycle
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [LogCategory(ReportCategory.SCENE_LOADING)]
    public partial class SceneLifeCycleGroup { }
}
