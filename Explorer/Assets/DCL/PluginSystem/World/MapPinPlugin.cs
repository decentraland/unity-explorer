using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.FeatureFlags;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.MapPins.Systems;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PluginSystem.World
{
    public class MapPinPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly Arch.Core.World globalWorld;
        private readonly FeatureFlagsCache featureFlagsCache;

        public MapPinPlugin(Arch.Core.World globalWorld, FeatureFlagsCache featureFlagsCache)
        {
            this.globalWorld = globalWorld;
            this.featureFlagsCache = featureFlagsCache;
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            //If the Map Pins feature is enabled or if it's a global PX we allow the Map Pins systems to run in them.
            if (featureFlagsCache.Configuration.IsEnabled(FeatureFlagsConfiguration.GetFlag(FeatureFlags.FeatureFlags.MAP_PINS)) ||
                sharedDependencies.SceneData.SceneEntityDefinition.metadata.isPortableExperience)
            {
                ResetDirtyFlagSystem<PBMapPin>.InjectToWorld(ref builder);
                MapPinLoaderSystem.InjectToWorld(ref builder, sharedDependencies.SceneData, globalWorld, sharedDependencies.ScenePartition, featureFlagsCache);
            }
        }
    }
}
