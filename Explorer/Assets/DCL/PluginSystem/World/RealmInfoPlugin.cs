using Arch.SystemGroups;
using DCL.Multiplayer.Connections.PortableExperiences;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.RealmInfo;
using ECS;
using ECS.LifeCycle;
using System.Collections.Generic;

namespace DCL.PluginSystem.World
{
    public class RealmInfoPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly IRealmData realmData;
        private readonly IRoomHub roomHub;
        private readonly PortableExperienceWorldComms portableExperienceWorldComms;

        public RealmInfoPlugin(IRealmData realmData, IRoomHub roomHub, PortableExperienceWorldComms portableExperienceWorldComms)
        {
            this.realmData = realmData;
            this.roomHub = roomHub;
            this.portableExperienceWorldComms = portableExperienceWorldComms;
        }

        public void Dispose()
        {
            //ignore
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in SystemsDependencies systemsDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            WriteRealmInfoSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter, realmData, roomHub, sharedDependencies.SceneData, portableExperienceWorldComms);
        }
    }
}
