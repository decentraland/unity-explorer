using Arch.Core;
using Arch.SystemGroups;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.AvatarLocomotion.Systems;
using ECS.LifeCycle;
using System.Collections.Generic;

namespace DCL.SDKComponents.AvatarLocomotion
{
    public class AvatarLocomotionOverridesWorldPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly World globalWorld;
        private readonly Entity globalPlayerEntity;

        public AvatarLocomotionOverridesWorldPlugin(World globalWorld, Entity globalPlayerEntity)
        {
            this.globalWorld = globalWorld;
            this.globalPlayerEntity = globalPlayerEntity;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder,
            in ECSWorldInstanceSharedDependencies sharedDependencies,
            in PersistentEntities persistentEntities,
            List<IFinalizeWorldSystem> finalizeWorldSystems,
            List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            var propagateSystem = PropagateAvatarLocomotionOverridesSystem.InjectToWorld(ref builder, sharedDependencies.SceneStateProvider, globalWorld, globalPlayerEntity);
            sceneIsCurrentListeners.Add(propagateSystem);
        }
    }
}
