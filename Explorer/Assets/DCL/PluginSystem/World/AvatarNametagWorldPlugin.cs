using Arch.Core;
using Arch.SystemGroups;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.AvatarNametag.Systems;
using ECS.LifeCycle;
using System.Collections.Generic;

namespace DCL.SDKComponents.AvatarNametag
{
    public class AvatarNametagWorldPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly World globalWorld;

        public AvatarNametagWorldPlugin(World globalWorld)
        {
            this.globalWorld = globalWorld;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder,
            in ECSWorldInstanceSharedDependencies sharedDependencies,
            in SystemsDependencies systemsDependencies, in PersistentEntities persistentEntities,
            List<IFinalizeWorldSystem> finalizeWorldSystems,
            List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            // Portable experiences follow the player across scenes, so they are refused the plate outright.
            if (sharedDependencies.SceneData.IsPortableExperience())
                return;

            var propagateSystem = PropagateSceneAvatarTagSystem.InjectToWorld(ref builder, sharedDependencies.SceneStateProvider,
                sharedDependencies.EntitiesMap, globalWorld);

            sceneIsCurrentListeners.Add(propagateSystem);
            finalizeWorldSystems.Add(propagateSystem);
        }
    }
}
