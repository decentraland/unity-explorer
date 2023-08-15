using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Diagnostics.ReportsHandling;

namespace DCL.Input
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [LogCategory(ReportCategory.INPUT)]
    public partial class InputGroup { }
}
