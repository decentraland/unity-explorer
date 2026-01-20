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
        private readonly AssetLoadCache assetLoadCache;
        private readonly ECSWorldSingletonSharedDependencies globalDeps;

        public AssetPreLoadPlugin(ECSWorldSingletonSharedDependencies globalDeps,
            AssetLoadCache assetLoadCache)
        {
            this.globalDeps = globalDeps;
            this.assetLoadCache = assetLoadCache;
        }

        public void Dispose()
        {
            assetLoadCache.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder,
            in ECSWorldInstanceSharedDependencies sharedDependencies,
            in PersistentEntities persistentEntities,
            List<IFinalizeWorldSystem> finalizeWorldSystems,
            List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            AssetPreLoadUtils utilsClass = new AssetPreLoadUtils(sharedDependencies.EcsToCRDTWriter, sharedDependencies.SceneStateProvider);

            AssetPreLoadSystem.InjectToWorld(ref builder, sharedDependencies.SceneData, globalDeps.FrameTimeBudget, utilsClass);
            FinalizeAssetPreLoadSystem.InjectToWorld(ref builder, globalDeps.FrameTimeBudget, assetLoadCache, utilsClass);

            finalizeWorldSystems.Add(CleanUpAssetPreLoadSystem.InjectToWorld(ref builder, assetLoadCache));
        }
    }
}
