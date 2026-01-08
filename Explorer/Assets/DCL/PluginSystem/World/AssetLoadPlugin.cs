using Arch.SystemGroups;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.AssetLoad.Systems;
using ECS.LifeCycle;
using ECS.Unity.GLTFContainer.Asset.Cache;
using System.Collections.Generic;

namespace DCL.PluginSystem.World
{
    public class AssetLoadPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly IGltfContainerAssetsCache assetsCache;
        private readonly ECSWorldSingletonSharedDependencies globalDeps;

        public AssetLoadPlugin(ECSWorldSingletonSharedDependencies globalDeps,
            IGltfContainerAssetsCache assetsCache)
        {
            this.globalDeps = globalDeps;
            this.assetsCache = assetsCache;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder,
            in ECSWorldInstanceSharedDependencies sharedDependencies,
            in PersistentEntities persistentEntities,
            List<IFinalizeWorldSystem> finalizeWorldSystems,
            List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            AssetLoadSystem.InjectToWorld(ref builder, sharedDependencies.SceneData, globalDeps.FrameTimeBudget);
            CleanUpAssetLoadSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter);
            FinalizeAssetLoadSystem.InjectToWorld(ref builder, globalDeps.FrameTimeBudget, assetsCache);
        }
    }
}
