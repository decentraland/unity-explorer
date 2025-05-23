using Arch.SystemGroups;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.RealmInfo;
using DCL.Utilities;
using ECS;
using ECS.LifeCycle;
using System.Collections.Generic;

namespace DCL.PluginSystem.World
{
    public class RealmInfoPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly IRealmData realmData;
        private readonly ObjectProxy<IRoomHub> roomHubProxy;

        public RealmInfoPlugin(IRealmData realmData, ObjectProxy<IRoomHub> roomHubProxy)
        {
            this.realmData = realmData;
            this.roomHubProxy = roomHubProxy;
        }

        public void Dispose()
        {
            //ignore
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            WriteRealmInfoSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter, realmData, roomHubProxy, sharedDependencies.SceneData);
        }
    }
}
