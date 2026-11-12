using Arch.SystemGroups;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.SceneWearables.Systems;
using ECS.LifeCycle;
using System.Collections.Generic;

namespace DCL.PluginSystem.World
{
    public class SceneWearablesPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly IWearableStorage wearableStorage;
        private readonly Arch.Core.World globalWorld;
        private readonly bool localSceneDevelopment;
        private readonly bool useLocalAssetBundles;

        public SceneWearablesPlugin(IWearableStorage wearableStorage, Arch.Core.World globalWorld, bool localSceneDevelopment, bool useLocalAssetBundles)
        {
            this.wearableStorage = wearableStorage;
            this.globalWorld = globalWorld;
            this.localSceneDevelopment = localSceneDevelopment;
            this.useLocalAssetBundles = useLocalAssetBundles;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in SystemsDependencies systemsDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            if (sharedDependencies.SceneData.IsPortableExperience())
                return;

            finalizeWorldSystems.Add(ResolveSceneWearablePathsSystem.InjectToWorld(ref builder, sharedDependencies.SceneData, wearableStorage,
                localSceneDevelopment, useLocalAssetBundles));

            PropagateSceneWearablesToAvatarsSystem propagateSystem = PropagateSceneWearablesToAvatarsSystem.InjectToWorld(ref builder, sharedDependencies.SceneStateProvider,
                sharedDependencies.EntitiesMap, globalWorld);
            sceneIsCurrentListeners.Add(propagateSystem);
            finalizeWorldSystems.Add(propagateSystem);
        }
    }
}
