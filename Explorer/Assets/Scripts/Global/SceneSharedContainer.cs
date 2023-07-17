using CRDT.Serializer;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Engine;
using Diagnostics;
using Diagnostics.ReportsHandling;
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
        public ISceneFactory SceneFactory { get; internal init; }

        public DiagnosticsContainer DiagnosticsContainer { get; internal init; }

        public static SceneSharedContainer Create(in ComponentsContainer componentsContainer, IReportsHandlingSettings reportsHandlingSettings)
        {
            var entityFactory = new EntityFactory();

            var sharedDependencies = new ECSWorldSingletonSharedDependencies(componentsContainer.ComponentPoolsRegistry, reportsHandlingSettings, entityFactory, new ConcurrentLoadingBudgetProvider(100));

            var ecsWorldFactory = new ECSWorldFactory(sharedDependencies,
                new TransformsPlugin(sharedDependencies),
                new MaterialsPlugin(),
                new PrimitiveCollidersPlugin(sharedDependencies),
                new TexturesLoadingPlugin(sharedDependencies.LoadingBudgetProvider),
                new PrimitivesRenderingPlugin(sharedDependencies),
                new VisibilityPlugin(),
                new AssetBundlesPlugin(reportsHandlingSettings, sharedDependencies.LoadingBudgetProvider),
                new GltfContainerPlugin(sharedDependencies));

            return new SceneSharedContainer
            {
                SceneFactory = new SceneFactory(
                    ecsWorldFactory,
                    new SceneRuntimeFactory(),
                    new SharedPoolsProvider(),
                    new CRDTSerializer(),
                    componentsContainer.SDKComponentsRegistry,
                    entityFactory
                ),
                DiagnosticsContainer = DiagnosticsContainer.Create(reportsHandlingSettings),
            };
        }

        public void Dispose()
        {
            DiagnosticsContainer.Dispose();
        }
    }
}
