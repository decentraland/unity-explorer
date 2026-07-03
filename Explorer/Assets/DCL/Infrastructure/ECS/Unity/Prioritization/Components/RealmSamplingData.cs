using Arch.SystemGroups.DefaultSystemGroups;
using System.Collections.Generic;

namespace ECS.Prioritization.Components
{
    public class RealmSamplingData : PartitionDiscreteDataBase
    {
        public readonly List<IPartitionedWorldsAggregate> Aggregates = new (SystemGroupsUtils.Count);
    }
}
