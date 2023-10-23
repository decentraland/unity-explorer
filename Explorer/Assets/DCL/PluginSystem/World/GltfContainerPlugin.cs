using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.Interaction.Utility;
using DCL.PluginSystem.World.Dependencies;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Systems;
using ECS.Unity.GLTFContainer.Systems;
using Global;
using System.Collections.Generic;

namespace DCL.PluginSystem.World
{
    public class GltfContainerPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly ECSWorldSingletonSharedDependencies globalDeps;
        private readonly MemoryBudgetProvider memoryBudgetProvider;
        private readonly GltfContainerAssetsCache assetsCache;

        public GltfContainerPlugin(ECSWorldSingletonSharedDependencies globalDeps, MemoryBudgetProvider memoryBudgetProvider, CacheCleaner cacheCleaner)
        {
            this.globalDeps = globalDeps;
            this.memoryBudgetProvider = memoryBudgetProvider;
            assetsCache = new GltfContainerAssetsCache(1000);

            // cacheCleaner.Register(assetsCache);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder,
            in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            // Asset loading
            PrepareGltfAssetLoadingSystem.InjectToWorld(ref builder, assetsCache);
            CreateGltfAssetFromAssetBundleSystem.InjectToWorld(ref builder, globalDeps.FrameTimeBudgetProvider, memoryBudgetProvider);
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
            CreateGltfAssetFromAssetBundleSystem.InjectToWorld(ref builder, globalDeps.FrameTimeBudgetProvider, memoryBudgetProvider);
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
