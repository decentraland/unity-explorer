using Arch.Core;
using Arch.SystemGroups;
using DCL.ECSComponents;
using Diagnostics.ReportsHandling;
using ECS.BudgetProvider;
using ECS.ComponentsPooling;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Systems;
using ECS.Unity.GLTFContainer.Systems;
using System.Collections.Generic;

namespace SceneRunner.ECSWorld.Plugins
{
    public class GltfContainerPlugin : IECSWorldPlugin
    {
        private readonly GltfContainerAssetsCache assetsCache;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly IReportsHandlingSettings reportsHandlingSettings;
        private readonly IConcurrentBudgetProvider frameTimeBudgetProvider;

        public GltfContainerPlugin(ECSWorldSingletonSharedDependencies singletonSharedDependencies)
        {
            componentPoolsRegistry = singletonSharedDependencies.ComponentPoolsRegistry;
            reportsHandlingSettings = singletonSharedDependencies.ReportsHandlingSettings;
            assetsCache = new GltfContainerAssetsCache(1000);
            frameTimeBudgetProvider = singletonSharedDependencies.InstantiationFrameBudgetProvider;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder,
            in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            // Asset loading
            PrepareGltfAssetLoadingSystem.InjectToWorld(ref builder, assetsCache);
            CreateGltfAssetFromAssetBundleSystem.InjectToWorld(ref builder, frameTimeBudgetProvider);
            ReportGltfErrorsSystem.InjectToWorld(ref builder, reportsHandlingSettings);

            // GLTF Container
            LoadGltfContainerSystem.InjectToWorld(ref builder);
            FinalizeGltfContainerLoadingSystem.InjectToWorld(ref builder, persistentEntities.SceneRoot);

            ResetGltfContainerSystem.InjectToWorld(ref builder, assetsCache);
            WriteGltfContainerLoadingStateSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter, componentPoolsRegistry.GetReferenceTypePool<PBGltfContainerLoadingState>());

            ResetDirtyFlagSystem<PBGltfContainer>.InjectToWorld(ref builder);

            var cleanUpGltfContainerSystem =
                CleanUpGltfContainerSystem.InjectToWorld(ref builder, assetsCache);

            finalizeWorldSystems.Add(cleanUpGltfContainerSystem);
        }
    }
}
