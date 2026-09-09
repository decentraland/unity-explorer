using Arch.Core;
using Arch.SystemGroups;
using DCL.Multiplayer.Profiles.Tables;
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
        private readonly Entity globalPlayerEntity;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;

        public AvatarNametagWorldPlugin(World globalWorld, Entity globalPlayerEntity, IReadOnlyEntityParticipantTable entityParticipantTable)
        {
            this.globalWorld = globalWorld;
            this.globalPlayerEntity = globalPlayerEntity;
            this.entityParticipantTable = entityParticipantTable;
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
                entityParticipantTable, globalWorld, globalPlayerEntity);

            sceneIsCurrentListeners.Add(propagateSystem);
            finalizeWorldSystems.Add(propagateSystem);
        }
    }
}
