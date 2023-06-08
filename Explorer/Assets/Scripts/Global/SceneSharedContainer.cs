using CRDT.Serializer;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Engine;
using SceneRunner;
using SceneRunner.ECSWorld;
using SceneRunner.ECSWorld.Plugins;
using SceneRuntime.Factory;

namespace Global
{
    /// <summary>
    ///     Holds dependencies shared between all scene instances. <br />
    ///     Consider breaking down this class as much as needed if the number of dependencies grows
    /// </summary>
    public class SceneSharedContainer
    {
        public ISceneFactory SceneFactory { get; internal init; }

        public static SceneSharedContainer Create(in ComponentsContainer componentsContainer)
        {
            var sharedDependencies = new ECSWorldSingletonSharedDependencies(componentsContainer.ComponentPoolsRegistry);

            var ecsWorldFactory = new ECSWorldFactory(sharedDependencies,
                new TransformsPlugin(sharedDependencies),
                new MaterialsPlugin(),
                new PrimitiveCollidersPlugin(sharedDependencies),
                new StreamableLoadingPlugin(),
                new PrimitivesRenderingPlugin(sharedDependencies),
                new VisibilityPlugin(),
                new AssetBundlesPlugin());

            return new SceneSharedContainer
            {
                SceneFactory = new SceneFactory(
                    ecsWorldFactory,
                    new SceneRuntimeFactory(),
                    new SharedPoolsProvider(),
                    new CRDTSerializer(),
                    componentsContainer.SDKComponentsRegistry,
                    new EntityFactory()
                ),
            };
        }
    }
}
