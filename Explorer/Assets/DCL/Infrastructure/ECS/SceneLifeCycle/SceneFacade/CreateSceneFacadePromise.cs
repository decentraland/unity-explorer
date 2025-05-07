using Arch.Core;
using DCL.Ipfs;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using SceneRunner.Scene;

namespace ECS.SceneLifeCycle.SceneFacade
{
    public static class CreateSceneFacadePromise
    {
        public static void Execute(World world, Entity entity, IIpfsRealm ipfsRealm, in SceneDefinitionComponent definitionComponent, IPartitionComponent partitionComponent)
        {
            world.Add(entity,
                AssetPromise<ISceneFacade, GetSceneFacadeIntention>.Create(world,
                    new GetSceneFacadeIntention(ipfsRealm, definitionComponent), partitionComponent));
        }
    }
}
