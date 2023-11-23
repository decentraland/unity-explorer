using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.Interaction.Utility;
using DCL.PerformanceBudgeting;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Systems;
using ECS.Unity.GLTFContainer.Systems;
using System.Collections.Generic;

namespace DCL.PluginSystem.World
{
    public class GltfContainerPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly ECSWorldSingletonSharedDependencies globalDeps;
        private readonly GltfContainerAssetsCache assetsCache;

        public GltfContainerPlugin(ECSWorldSingletonSharedDependencies globalDeps, IConcurrentBudgetProvider frameTimeBudgetProvider, CacheCleaner cacheCleaner)
        {
            this.globalDeps = globalDeps;
            assetsCache = new GltfContainerAssetsCache(frameTimeBudgetProvider);

            cacheCleaner.Register(assetsCache);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder,
            in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            // Asset loading
            PrepareGltfAssetLoadingSystem.InjectToWorld(ref builder, assetsCache);
            CreateGltfAssetFromAssetBundleSystem.InjectToWorld(ref builder, globalDeps.FrameTimeBudgetProvider, globalDeps.MemoryBudgetProvider);
            ReportGltfErrorsSystem.InjectToWorld(ref builder, globalDeps.ReportsHandlingSettings);

            // GLTF Container
            LoadGltfContainerSystem.InjectToWorld(ref builder);
            FinalizeGltfContainerLoadingSystem.InjectToWorld(ref builder, persistentEntities.SceneRoot, globalDeps.FrameTimeBudgetProvider, sharedDependencies.EntityCollidersSceneCache);

            ResetGltfContainerSystem.InjectToWorld(ref builder, assetsCache, sharedDependencies.EntityCollidersSceneCache);
            WriteGltfContainerLoadingStateSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter, globalDeps.ComponentPoolsRegistry.GetReferenceTypePool<PBGltfContainerLoadingState>());

            ResetDirtyFlagSystem<PBGltfContainer>.InjectToWorld(ref builder);

            var cleanUpGltfContainerSystem =
                CleanUpGltfContainerSystem.InjectToWorld(ref builder, assetsCache, sharedDependencies.EntityCollidersSceneCache);

            finalizeWorldSystems.Add(cleanUpGltfContainerSystem);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies)
        {
            // Asset loading
            PrepareGltfAssetLoadingSystem.InjectToWorld(ref builder, assetsCache);
            CreateGltfAssetFromAssetBundleSystem.InjectToWorld(ref builder, globalDeps.FrameTimeBudgetProvider, globalDeps.MemoryBudgetProvider);
            ReportGltfErrorsSystem.InjectToWorld(ref builder, globalDeps.ReportsHandlingSettings);

            // GLTF Container
            LoadGltfContainerSystem.InjectToWorld(ref builder);
            FinalizeGltfContainerLoadingSystem.InjectToWorld(ref builder, dependencies.SceneRoot, globalDeps.FrameTimeBudgetProvider, NullEntityCollidersSceneCache.INSTANCE);

            ResetGltfContainerSystem.InjectToWorld(ref builder, assetsCache, NullEntityCollidersSceneCache.INSTANCE);

            ResetDirtyFlagSystem<PBGltfContainer>.InjectToWorld(ref builder);

            CleanUpGltfContainerSystem.InjectToWorld(ref builder, assetsCache, NullEntityCollidersSceneCache.INSTANCE);
        }
    }
}
