using CrdtEcsBridge.Components;
using Diagnostics;
using Diagnostics.ReportsHandling;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.Profiling;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using SceneRunner.ECSWorld;
using SceneRunner.ECSWorld.Plugins;
using System;
using System.Collections.Generic;

namespace Global
{
    /// <summary>
    ///     Produces dependencies that never change during the lifetime of the application
    ///     and are not connected to the global world or scenes but are used by them.
    ///     This is the first container to instantiate, should not depend on any other container
    /// </summary>
    public class StaticContainer : IDisposable
    {
        public DiagnosticsContainer DiagnosticsContainer { get; private set; }

        public ComponentsContainer ComponentsContainer { get; private set; }

        public IPartitionSettings PartitionSettings { get; private set; }

        public CameraSamplingData CameraSamplingData { get; private set; }

        public IReadOnlyList<IECSWorldPlugin> ECSWorldPlugins { get; private set; }

        public ECSWorldSingletonSharedDependencies SingletonSharedDependencies { get; private set; }

        public IProfilingProvider ProfilingProvider { get; private set; }

        public void Dispose()
        {
            DiagnosticsContainer?.Dispose();
        }

        public static StaticContainer Create(IPartitionSettings partitionSettings, IReportsHandlingSettings reportsHandlingSettings)
        {
            var componentsContainer = ComponentsContainer.Create();
            var profilingProvider = new ProfilingProvider();

            var sharedDependencies = new ECSWorldSingletonSharedDependencies(
                componentsContainer.ComponentPoolsRegistry,
                reportsHandlingSettings,
                new EntityFactory(),
                new PartitionedWorldsAggregate.Factory(),
                new ConcurrentLoadingBudgetProvider(50),
                new FrameTimeCapBudgetProvider(40, profilingProvider)
            );

            return new StaticContainer
            {
                DiagnosticsContainer = DiagnosticsContainer.Create(reportsHandlingSettings),
                ComponentsContainer = componentsContainer,
                PartitionSettings = partitionSettings,
                SingletonSharedDependencies = sharedDependencies,
                CameraSamplingData = new CameraSamplingData(),
                ProfilingProvider = profilingProvider,
                ECSWorldPlugins = new IECSWorldPlugin[]
                {
                    new TransformsPlugin(sharedDependencies),
                    new MaterialsPlugin(sharedDependencies),
                    new PrimitiveCollidersPlugin(sharedDependencies),
                    new TexturesLoadingPlugin(),
                    new PrimitivesRenderingPlugin(sharedDependencies),
                    new VisibilityPlugin(),
                    new AssetBundlesPlugin(reportsHandlingSettings),
                    new GltfContainerPlugin(sharedDependencies),
                },
            };
        }
    }
}
