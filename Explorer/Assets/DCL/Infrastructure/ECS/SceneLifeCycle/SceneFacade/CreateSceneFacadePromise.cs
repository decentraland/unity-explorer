using Arch.Core;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using SceneRunner.Scene;

namespace ECS.SceneLifeCycle.SceneFacade
{
    public static class CreateSceneFacadePromise
    {
        public static void Execute(World world, Entity entity, in SceneDefinitionComponent definitionComponent, IPartitionComponent partitionComponent)
        {
            world.Add(entity,
                AssetPromise<ISceneFacade, GetSceneFacadeIntention>.Create(world,
                    new GetSceneFacadeIntention(definitionComponent), partitionComponent));
        }
    }
}
