using CRDT.Serializer;
using CrdtEcsBridge.Engine;
using SceneRunner;
using SceneRunner.ECSWorld;
using SceneRuntime.Factory;
using System;

namespace Global
{
    /// <summary>
    ///     Holds dependencies shared between all scene instances. <br />
    ///     Consider breaking down this class as much as needed if the number of dependencies grows
    /// </summary>
    public class SceneSharedContainer : IDisposable
    {
        public ISceneFactory SceneFactory { get; private set; }

        public void Dispose() { }

        public static SceneSharedContainer Create(in StaticContainer staticContainer)
        {
            ECSWorldSingletonSharedDependencies sharedDependencies = staticContainer.SingletonSharedDependencies;

            var ecsWorldFactory = new ECSWorldFactory(sharedDependencies,
                staticContainer.PartitionSettings,
                staticContainer.CameraSamplingData,
                staticContainer.ECSWorldPlugins);

            return new SceneSharedContainer
            {
                SceneFactory = new SceneFactory(
                    ecsWorldFactory,
                    new SceneRuntimeFactory(),
                    new SharedPoolsProvider(),
                    new CRDTSerializer(),
                    staticContainer.ComponentsContainer.SDKComponentsRegistry,
                    sharedDependencies.EntityFactory
                ),
            };
        }
    }
}
