using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Utilities;
using ECS;
using ECS.Abstract;
using ECS.Groups;

namespace DCL.SDKComponents.RealmInfo
{
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    public partial class WriteRealmInfoSystem : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly ObjectProxy<IRealmData> realmDataProxy;
        private readonly ObjectProxy<IRoomHub> roomHubProxy;

        internal WriteRealmInfoSystem(World world, IECSToCRDTWriter ecsToCRDTWriter, ObjectProxy<IRealmData> realmDataProxy, ObjectProxy<IRoomHub> roomHubProxy) : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.realmDataProxy = realmDataProxy;
            this.roomHubProxy = roomHubProxy;
        }

        public override void Initialize()
        {
            PropagateToScene();
        }

        protected override void Update(float t)
        {
            if (!realmDataProxy.Object!.IsDirty)
                return;

            PropagateToScene();
        }

        private void PropagateToScene()
        {
            ecsToCRDTWriter.PutMessage<PBRealmInfo, (IRealmData realmData, IRoomHub roomHub)>(static (component, data) =>
            {
                component.BaseUrl = data.realmData.Ipfs.CatalystBaseUrl.Value;
                component.RealmName = data.realmData.RealmName;
                component.NetworkId = data.realmData.NetworkId;
                component.CommsAdapter = data.realmData.CommsAdapter;

                var room = data.roomHub.IslandRoom().Info.Sid;
                component.Room = string.IsNullOrEmpty(room) ? string.Empty : room;
                // component.IsPreview // TODO: when E@ supports running in preview mode

            }, SpecialEntitiesID.SCENE_ROOT_ENTITY, (realmDataProxy.Object, roomHubProxy.Object));
        }
    }
}
