using Arch.Core;
using Arch.SystemGroups;
using ECS.ComponentsPooling;
using ECS.Unity.Systems;

namespace SceneRunner.ECSWorld
{
    public class ECSWorldFactory : IECSWorldFactory
    {
        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        public ECSWorldFactory(IComponentPoolsRegistry componentPoolsRegistry /* Add here all singleton dependencies */)
        {
            this.componentPoolsRegistry = componentPoolsRegistry;
        }

        public ECSWorldFacade CreateWorld()
        {
            // Worlds uses Pooled Collections under the hood so the memory impact is minimized
            var world = World.Create();

            // Create all systems and add them to the world
            var builder = new ArchSystemsWorldBuilder<World>(world);
            UpdateTransformUnitySystem.InjectToWorld(ref builder);
            InstantiateTransformUnitySystem.InjectToWorld(ref builder, componentPoolsRegistry);
            var releaseSDKComponentsSystem = ReleaseSDKComponentsSystem.InjectToWorld(ref builder, componentPoolsRegistry);

            // Add other systems here
            var systemsWorld = builder.Finish();

            return new ECSWorldFacade(systemsWorld, world, releaseSDKComponentsSystem);
        }
    }
}
