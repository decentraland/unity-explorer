using Arch.Core;
using Arch.SystemGroups;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.AvatarLocomotion.Systems;
using ECS.LifeCycle;
using System.Collections.Generic;

namespace DCL.SDKComponents.AvatarLocomotion
{
    public class AvatarLocomotionSettingsPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly World globalWorld;

        public AvatarLocomotionSettingsPlugin(World globalWorld)
        {
            this.globalWorld = globalWorld;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder,
            in ECSWorldInstanceSharedDependencies sharedDependencies,
            in PersistentEntities persistentEntities,
            List<IFinalizeWorldSystem> finalizeWorldSystems,
            List<ISceneIsCurrentListener> sceneIsCurrentListeners) =>
            PropagateAvatarLocomotionSettingsSystem.InjectToWorld(ref builder, sharedDependencies.SceneStateProvider, globalWorld);
    }
}
