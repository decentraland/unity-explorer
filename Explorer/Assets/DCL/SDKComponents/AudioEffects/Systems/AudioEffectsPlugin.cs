using Arch.Core;
using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using System.Collections.Generic;

namespace DCL.SDKComponents.AudioEffects.Systems
{
    public class AudioEffectsPlugin : IDCLWorldPluginWithoutSettings
    {
        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder,
            in ECSWorldInstanceSharedDependencies sharedDependencies,
            in PersistentEntities persistentEntities,
            List<IFinalizeWorldSystem> finalizeWorldSystems,
            List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            var registry = new SceneAudioEffectsRegistry();

            ResetDirtyFlagSystem<PBAudioSourceEffect>.InjectToWorld(ref builder);

            var aggregator = AudioSourceEffectAggregatorSystem.InjectToWorld(ref builder, registry);
            finalizeWorldSystems.Add(aggregator);
        }
    }
}
