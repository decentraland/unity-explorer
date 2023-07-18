using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.DeferredLoading;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using Ipfs;
using SceneRunner.Scene;

namespace ECS.SceneLifeCycle.DeferredLoading
{
    /// <summary>
    ///     Weighs definitions and scenes loading against each other according to their partition
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(LoadSceneDefinitionListSystem))]
    [UpdateBefore(typeof(LoadSceneSystem))]
    [UpdateBefore(typeof(LoadSceneDefinitionSystem))]
    public partial class SceneLifeCycleDeferredLoadingSystem : DeferredLoadingSystem
    {
        private static readonly QueryDescription[] COMPONENT_HANDLERS;

        static SceneLifeCycleDeferredLoadingSystem()
        {
            COMPONENT_HANDLERS = new[]
            {
                CreateQuery<GetSceneDefinitionList, SceneDefinitions>(),
                CreateQuery<GetSceneDefinition, IpfsTypes.SceneEntityDefinition>(),
                CreateQuery<GetSceneFacadeIntention, ISceneFacade>(),
            };
        }

        internal SceneLifeCycleDeferredLoadingSystem(World world, IConcurrentBudgetProvider concurrentLoadingBudgetProvider)
            : base(world, COMPONENT_HANDLERS, concurrentLoadingBudgetProvider) { }
    }
}
