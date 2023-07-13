using CRDT.Serializer;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Engine;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using SceneRunner;
using SceneRunner.ECSWorld;
using SceneRunner.ECSWorld.Plugins;
using SceneRuntime.Factory;
using System;
using UnityEngine;

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

        public static SceneSharedContainer Create(in StaticContainer staticContainer, AssetBundleManifest localAssetBundleManifest)
        {
            var entityFactory = new EntityFactory();

            var sharedDependencies = new ECSWorldSingletonSharedDependencies(staticContainer.ComponentsContainer.ComponentPoolsRegistry,
                staticContainer.ReportsHandlingSettings,
                entityFactory,
                staticContainer.WorldsAggregateFactory,
                new ConcurrentLoadingBudgetProvider(100));

            var ecsWorldFactory = new ECSWorldFactory(sharedDependencies,
                staticContainer.PartitionSettings,
                staticContainer.CameraSamplingData,
                new TransformsPlugin(sharedDependencies),
                new MaterialsPlugin(),
                new PrimitiveCollidersPlugin(sharedDependencies),
                new TexturesLoadingPlugin(sharedDependencies.LoadingBudgetProvider),
                new PrimitivesRenderingPlugin(sharedDependencies),
                new VisibilityPlugin(),
                new AssetBundlesPlugin(localAssetBundleManifest, staticContainer.ReportsHandlingSettings, sharedDependencies.LoadingBudgetProvider),
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
