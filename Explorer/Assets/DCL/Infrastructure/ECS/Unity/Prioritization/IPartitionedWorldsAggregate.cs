using Arch.SystemGroups;
using ECS.Prioritization.Components;

namespace ECS.Prioritization
{
    public interface IPartitionedWorldsAggregate : ISystemGroupAggregate<IPartitionComponent>
    {
        /// <summary>
        ///     Sorts all worlds based on their partition data. It is expensive call and its invocation should be amortized.
        /// </summary>
        void UpdateSorting();
    }
}
