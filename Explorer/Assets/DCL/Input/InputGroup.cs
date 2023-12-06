using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;

namespace DCL.Input
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [LogCategory(ReportCategory.INPUT)]
    public partial class InputGroup { }
}
