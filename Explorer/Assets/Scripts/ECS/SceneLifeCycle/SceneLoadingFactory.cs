using Arch.Core;
using DCL.Ipfs;
using DCL.LOD.Components;
using DCL.Roads.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.SceneFacade;

namespace ECS.SceneLifeCycle
{
    public static class SceneLoadingFactory
    {
        public static void CreateVisualScene(World world, in Entity entity, in VisualSceneStateEnum sceneState, in IIpfsRealm ipfsRealm, in SceneDefinitionComponent sceneDefinitionComponent,
            in PartitionComponent partitionComponent)
        {
            switch (sceneState)
            {
                case VisualSceneStateEnum.SHOWING_LOD:
                    world.Add(entity, SceneLODInfo.Create());
                    break;
                case VisualSceneStateEnum.ROAD:
                    world.Add(entity, RoadInfo.Create());
                    break;
                default:
                    CreateSceneFacadePromise.Execute(world, entity, ipfsRealm, in sceneDefinitionComponent, partitionComponent);
                    break;
            }
        }
    }
}
