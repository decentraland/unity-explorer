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

        public AssetPreLoadPlugin(ECSWorldSingletonSharedDependencies globalDeps,
            AssetPreLoadCache assetPreLoadCache)
        {
            this.globalDeps = globalDeps;
            this.assetPreLoadCache = assetPreLoadCache;
        }

        public void Dispose()
        {
            assetPreLoadCache.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder,
            in ECSWorldInstanceSharedDependencies sharedDependencies,
            in PersistentEntities persistentEntities,
            List<IFinalizeWorldSystem> finalizeWorldSystems,
            List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            AssetPreLoadSystem.InjectToWorld(ref builder, sharedDependencies.SceneData, globalDeps.FrameTimeBudget);
            FinalizeAssetPreLoadSystem.InjectToWorld(ref builder, globalDeps.FrameTimeBudget, assetPreLoadCache);
            HandleAssetPreLoadUpdates.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter, sharedDependencies.SceneStateProvider);

            finalizeWorldSystems.Add(CleanUpAssetPreLoadSystem.InjectToWorld(ref builder, assetPreLoadCache));
        }
    }
}
