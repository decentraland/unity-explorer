using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.FeatureFlags;
using DCL.MapPins.Bus;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.MapPins.Systems;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using System.Collections.Generic;

namespace DCL.PluginSystem.World
{
    public class MapPinPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly Arch.Core.World globalWorld;
        private readonly IMapPinsEventBus mapPinsEventBus;

        public MapPinPlugin(Arch.Core.World globalWorld, IMapPinsEventBus mapPinsEventBus)
        {
            this.globalWorld = globalWorld;
            this.mapPinsEventBus = mapPinsEventBus;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            //If the Map Pins feature is enabled or if it's a global PX we allow the Map Pins systems to run in them.
            if (FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.MAP_PINS) ||
                sharedDependencies.SceneData.SceneEntityDefinition.metadata.isPortableExperience)
            {
                ResetDirtyFlagSystem<PBMapPin>.InjectToWorld(ref builder);
                finalizeWorldSystems.Add(MapPinLoaderSystem.InjectToWorld(ref builder, sharedDependencies.SceneData, globalWorld, sharedDependencies.ScenePartition, mapPinsEventBus));
            }
        }
    }
}
