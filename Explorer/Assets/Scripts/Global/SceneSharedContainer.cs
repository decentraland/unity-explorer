using CRDT.Serializer;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Engine;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using Global.Dynamic;
using SceneRunner;
using SceneRunner.ECSWorld;
using SceneRunner.ECSWorld.Plugins;
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

        public static SceneSharedContainer Create(in StaticContainer staticContainer, float instantiationframeBudget, float loadingFrameBudget)
        {
            var entityFactory = new EntityFactory();

            var sharedDependencies = new ECSWorldSingletonSharedDependencies(staticContainer.ComponentsContainer.ComponentPoolsRegistry,
                staticContainer.ReportsHandlingSettings,
                entityFactory,
                staticContainer.WorldsAggregateFactory,
                new ConcurrentLoadingBudgetProvider(10),
                new FrameTimeBudgetProvider(instantiationframeBudget,staticContainer.ProfilingProvider),
                new FrameTimeBudgetProvider(loadingFrameBudget, staticContainer.ProfilingProvider),
                new FrameTimeCapBudgetProvider(10, staticContainer.ProfilingProvider));

            var ecsWorldFactory = new ECSWorldFactory(sharedDependencies,
                staticContainer.PartitionSettings,
                staticContainer.CameraSamplingData,
                new TransformsPlugin(sharedDependencies),
                new MaterialsPlugin(sharedDependencies),
                new PrimitiveCollidersPlugin(sharedDependencies),
                new TexturesLoadingPlugin(sharedDependencies.LoadingFrameTimeBudgetProvider),
                new PrimitivesRenderingPlugin(sharedDependencies),
                new VisibilityPlugin(),
                new AssetBundlesPlugin(staticContainer.ReportsHandlingSettings, sharedDependencies.LoadingFrameTimeBudgetProvider),
                new GltfContainerPlugin(sharedDependencies));

            return new SceneSharedContainer
            {
                SceneFactory = new SceneFactory(
                    ecsWorldFactory,
                    new SceneRuntimeFactory(),
                    new SharedPoolsProvider(),
                    new CRDTSerializer(),
                    staticContainer.ComponentsContainer.SDKComponentsRegistry,
                    entityFactory
                ),
            };
        }
    }
}
