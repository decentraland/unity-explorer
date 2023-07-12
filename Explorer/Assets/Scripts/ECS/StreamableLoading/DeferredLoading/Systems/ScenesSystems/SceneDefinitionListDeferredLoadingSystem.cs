using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;

namespace ECS.StreamableLoading.DeferredLoading
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(LoadSceneDefinitionListSystem))]
    public partial class SceneDefinitionListDeferredLoadingSystem : DeferredLoadingSystem<SceneDefinitions, GetSceneDefinitionList>
    {
        public SceneDefinitionListDeferredLoadingSystem(World world, IConcurrentBudgetProvider concurrentLoadingBudgetProvider) : base(world, concurrentLoadingBudgetProvider) { }
    }
}
