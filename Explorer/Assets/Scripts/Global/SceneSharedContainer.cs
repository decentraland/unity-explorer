using CRDT.Serializer;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Engine;
using ECS.Prioritization.DeferredLoading;
using SceneRunner;
using SceneRunner.ECSWorld;
using SceneRunner.ECSWorld.Plugins;
using SceneRuntime.Factory;
using UnityEngine;

namespace Global
{
    /// <summary>
    ///     Holds dependencies shared between all scene instances. <br />
    ///     Consider breaking down this class as much as needed if the number of dependencies grows
    /// </summary>
    public class SceneSharedContainer
    {
        public ISceneFactory SceneFactory { get; internal init; }

        public static SceneSharedContainer Create(in ComponentsContainer componentsContainer, AssetBundleManifest localAssetBundleManifest)
        {
            var sharedDependencies = new ECSWorldSingletonSharedDependencies(componentsContainer.ComponentPoolsRegistry,
                new ConcurrentLoadingBudgetProvider(100), new ConcurrentLoadingBudgetProvider(100));

            var ecsWorldFactory = new ECSWorldFactory(sharedDependencies,
                new TransformsPlugin(sharedDependencies),
                new MaterialsPlugin(),
                new PrimitiveCollidersPlugin(sharedDependencies),
                new TexturesLoadingPlugin(sharedDependencies.LoadingBudgetProvider),
                new PrimitivesRenderingPlugin(sharedDependencies),
                new VisibilityPlugin(),
                new AssetBundlesPlugin(localAssetBundleManifest, sharedDependencies.LoadingBudgetProvider),
                new GltfContainerPlugin(sharedDependencies));

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
