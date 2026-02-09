using Arch.Core;
using CommunicationData.URLHelpers;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using SceneRunner.Scene;

namespace ECS.SceneLifeCycle.SceneFacade
{
    public static class CreateSceneFacadePromise
    {
        public static void Execute(World world, Entity entity, URLDomain contentBaseUrl, in SceneDefinitionComponent definitionComponent, IPartitionComponent partitionComponent)
        {
            world.Add(entity,
                AssetPromise<ISceneFacade, GetSceneFacadeIntention>.Create(world,
                    new GetSceneFacadeIntention(contentBaseUrl, definitionComponent), partitionComponent));
        }
    }
}
