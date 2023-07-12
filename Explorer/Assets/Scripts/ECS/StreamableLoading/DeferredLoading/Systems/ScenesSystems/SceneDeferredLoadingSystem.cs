using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.SceneLifeCycle;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using SceneRunner.Scene;

namespace ECS.StreamableLoading.DeferredLoading
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(LoadSceneSystem))]
    public partial class SceneDeferredLoadingSystem : DeferredLoadingSystem<ISceneFacade, GetSceneFacadeIntention>
    {
        public SceneDeferredLoadingSystem(World world, IConcurrentBudgetProvider concurrentLoadingBudgetProvider) : base(world, concurrentLoadingBudgetProvider) { }
    }
}
