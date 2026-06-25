using Arch.SystemGroups;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.RealmInfo;
using ECS;
using ECS.LifeCycle;
using ECS.SceneLifeCycle;
using System.Collections.Generic;

namespace DCL.PluginSystem.World
{
    public class RealmInfoPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly IRealmData realmData;
        private readonly IRoomHub roomHub;
        private readonly IScenesCache scenesCache;

        public RealmInfoPlugin(IRealmData realmData, IRoomHub roomHub, IScenesCache scenesCache)
        {
            this.realmData = realmData;
            this.roomHub = roomHub;
            this.scenesCache = scenesCache;
        }

        public void Dispose()
        {
            //ignore
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in SystemsDependencies systemsDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            WriteRealmInfoSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter, realmData, roomHub, sharedDependencies.SceneData, scenesCache);
        }
    }
}
