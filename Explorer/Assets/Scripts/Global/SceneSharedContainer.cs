using CRDT.Serializer;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.PoolsProviders;
using SceneRunner.ECSWorld;
using SceneRunner.Scene;
using SceneRuntime.Factory;

namespace Global
{
    /// <summary>
    /// Holds dependencies shared between all scene instances. <br/>
    /// Consider breaking down this class as much as needed if the number of dependencies grows
    /// </summary>
    public class SceneSharedContainer
    {
        public ISceneFactory SceneFactory { get; internal init; }

        public static SceneSharedContainer Create(in ComponentsContainer componentsContainer)
        {
            var ecsWorldFactory = new ECSWorldFactory(componentsContainer.ComponentPoolsRegistry);

            return new SceneSharedContainer
            {
                SceneFactory = new SceneFactory(
                    ecsWorldFactory,
                    new SceneRuntimeFactory(),
                    new SharedPoolsProvider(),
                    new CRDTSerializer(),
                    componentsContainer.SDKComponentsRegistry,
                    new EntityFactory()
                )
            };
        }
    }
}
