using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.RealmInfo;
using DCL.Utilities;
using ECS;
using ECS.LifeCycle;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PluginSystem.World
{
    public class RealmInfoPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly ObjectProxy<IRealmData> realmDataProxy;
        private readonly ObjectProxy<IRoomHub> roomHubProxy;

        public RealmInfoPlugin(ObjectProxy<IRealmData> realmDataProxy, ObjectProxy<IRoomHub> roomHubProxy)
        {
            this.realmDataProxy = realmDataProxy;
            this.roomHubProxy = roomHubProxy;
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;

        public void Dispose()
        {
            //ignore
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            WriteRealmInfoSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter, realmDataProxy, roomHubProxy);
        }
    }
}
