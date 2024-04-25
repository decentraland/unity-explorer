using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Systems;
using ECS.Unity.GLTFContainer.Systems;
using ECS.Unity.Visibility.Systems;
using System.Collections.Generic;

namespace DCL.PluginSystem.World
{
    public class GltfContainerPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly GltfContainerAssetsCache assetsCache;
        private readonly ECSWorldSingletonSharedDependencies globalDeps;

        public GltfContainerPlugin(ECSWorldSingletonSharedDependencies globalDeps, CacheCleaner cacheCleaner)
        {
            this.globalDeps = globalDeps;
            assetsCache = new GltfContainerAssetsCache();

            cacheCleaner.Register(assetsCache);
        }

        public void Dispose()
        {
            assetsCache.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder,
            in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            // Asset loading
            PrepareGltfAssetLoadingSystem.InjectToWorld(ref builder, assetsCache);
            CreateGltfAssetFromAssetBundleSystem.InjectToWorld(ref builder, globalDeps.FrameTimeBudget, globalDeps.MemoryBudget);
            ReportGltfErrorsSystem.InjectToWorld(ref builder, globalDeps.ReportsHandlingSettings);

            // GLTF Container
            LoadGltfContainerSystem.InjectToWorld(ref builder);
            FinalizeGltfContainerLoadingSystem.InjectToWorld(ref builder, persistentEntities.SceneRoot, globalDeps.FrameTimeBudget, sharedDependencies.EntityCollidersSceneCache, sharedDependencies.SceneData);

            ResetGltfContainerSystem.InjectToWorld(ref builder, assetsCache, sharedDependencies.EntityCollidersSceneCache);
            WriteGltfContainerLoadingStateSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter);
            GltfContainerVisibilitySystem.InjectToWorld(ref builder);

            ResetDirtyFlagSystem<PBGltfContainer>.InjectToWorld(ref builder);

            var cleanUpGltfContainerSystem =
                CleanUpGltfContainerSystem.InjectToWorld(ref builder, assetsCache, sharedDependencies.EntityCollidersSceneCache);

            finalizeWorldSystems.Add(cleanUpGltfContainerSystem);
        }
    }
}
