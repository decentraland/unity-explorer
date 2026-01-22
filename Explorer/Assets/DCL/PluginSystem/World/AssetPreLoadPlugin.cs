using Arch.SystemGroups;
using ECS.Unity.AssetLoad;
using ECS.Unity.AssetLoad.Cache;
using ECS.Unity.AssetLoad.Systems;
using DCL.PluginSystem.World.Dependencies;
using ECS.LifeCycle;
using System.Collections.Generic;

namespace DCL.PluginSystem.World
{
    public class AssetPreLoadPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly AssetPreLoadCache assetPreLoadCache;
        private readonly ECSWorldSingletonSharedDependencies globalDeps;

        private AssetPreLoadUtils? utilsClass;

        public AssetPreLoadPlugin(ECSWorldSingletonSharedDependencies globalDeps,
            AssetPreLoadCache assetPreLoadCache)
        {
            this.globalDeps = globalDeps;
            this.assetPreLoadCache = assetPreLoadCache;
        }

        public void Dispose()
        {
            assetPreLoadCache.Dispose();
            utilsClass?.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder,
            in ECSWorldInstanceSharedDependencies sharedDependencies,
            in PersistentEntities persistentEntities,
            List<IFinalizeWorldSystem> finalizeWorldSystems,
            List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            utilsClass = new AssetPreLoadUtils();

            AssetPreLoadSystem.InjectToWorld(ref builder, sharedDependencies.SceneData, globalDeps.FrameTimeBudget, utilsClass);
            FinalizeAssetPreLoadSystem.InjectToWorld(ref builder, globalDeps.FrameTimeBudget, assetPreLoadCache, utilsClass);
            HandleAssetPreloadUpdates.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter, sharedDependencies.SceneStateProvider, utilsClass);

            finalizeWorldSystems.Add(CleanUpAssetPreLoadSystem.InjectToWorld(ref builder, assetPreLoadCache, utilsClass));
        }
    }
}
