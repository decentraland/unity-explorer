using Arch.Core;
using Arch.SystemGroups;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Multiplayer.Profiles.Tables;
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
        private readonly Entity globalPlayerEntity;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly bool loadFromLocalScene;

        public SceneWearablesPlugin(IWearableStorage wearableStorage, Arch.Core.World globalWorld, Entity globalPlayerEntity,
            IReadOnlyEntityParticipantTable entityParticipantTable, bool loadFromLocalScene)
        {
            this.wearableStorage = wearableStorage;
            this.globalWorld = globalWorld;
            this.globalPlayerEntity = globalPlayerEntity;
            this.entityParticipantTable = entityParticipantTable;
            this.loadFromLocalScene = loadFromLocalScene;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in SystemsDependencies systemsDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            if (sharedDependencies.SceneData.IsPortableExperience())
                return;

            finalizeWorldSystems.Add(ResolveSceneWearablePathsSystem.InjectToWorld(ref builder, sharedDependencies.SceneData, wearableStorage, loadFromLocalScene));

            PropagateSceneWearablesToAvatarsSystem propagateSystem = PropagateSceneWearablesToAvatarsSystem.InjectToWorld(ref builder, sharedDependencies.SceneData, sharedDependencies.SceneStateProvider,
                entityParticipantTable, globalWorld, globalPlayerEntity);
            sceneIsCurrentListeners.Add(propagateSystem);
            finalizeWorldSystems.Add(propagateSystem);
        }
    }
}
