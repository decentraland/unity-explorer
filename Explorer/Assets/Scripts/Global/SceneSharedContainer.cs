using CRDT.Serializer;
using CrdtEcsBridge.Engine;
using DCL.PluginSystem.World.Dependencies;
using SceneRunner;
using SceneRunner.ECSWorld;
using SceneRuntime.Factory;

namespace Global
{
    /// <summary>
    ///     Holds dependencies shared between all scene instances. <br />
    ///     Consider breaking down this class as much as needed if the number of dependencies grows
    /// </summary>
    public class SceneSharedContainer
    {
        public ISceneFactory SceneFactory { get; private set; }

        public static SceneSharedContainer Create(in StaticContainer staticContainer)
        {
            ECSWorldSingletonSharedDependencies sharedDependencies = staticContainer.SingletonSharedDependencies;
            ExposedGlobalDataContainer exposedGlobalDataContainer = staticContainer.ExposedGlobalDataContainer;

            var ecsWorldFactory = new ECSWorldFactory(sharedDependencies,
                staticContainer.PartitionSettings,
                exposedGlobalDataContainer.CameraSamplingData,
                exposedGlobalDataContainer.ExposedCameraData,
                staticContainer.ECSWorldPlugins);

            return new SceneSharedContainer
            {
                SceneFactory = new SceneFactory(
                    ecsWorldFactory,
                    new SceneRuntimeFactory(),
                    new SharedPoolsProvider(),
                    new CRDTSerializer(),
                    staticContainer.ComponentsContainer.SDKComponentsRegistry,
                    sharedDependencies.EntityFactory,
                    staticContainer.EntityCollidersGlobalCache
                ),
            };
        }
    }
}
