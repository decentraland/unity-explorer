using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using Ipfs;

namespace ECS.StreamableLoading.DeferredLoading
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(LoadSceneDefinitionSystem))]
    public partial class SceneDefinitionDeferredLoadingSystem : DeferredLoadingSystem<IpfsTypes.SceneEntityDefinition, GetSceneDefinition>
    {
        public SceneDefinitionDeferredLoadingSystem(World world, IConcurrentBudgetProvider concurrentLoadingBudgetProvider) : base(world, concurrentLoadingBudgetProvider) { }
    }
}
