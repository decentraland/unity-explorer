using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.Utilities;
using ECS;
using ECS.Abstract;
using ECS.Groups;
using SceneRunner.Scene;

namespace DCL.SDKComponents.RealmInfo
{
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    public partial class WriteRealmInfoSystem : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly IRealmData realmData;
        private readonly CommsRoomInfo commsRoomInfo;

        private bool initialized;

        internal WriteRealmInfoSystem(World world, IECSToCRDTWriter ecsToCRDTWriter, IRealmData realmData, ObjectProxy<IRoomHub> roomHubProxy, ISceneData sceneData)
            : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.realmData = realmData;

            commsRoomInfo = new CommsRoomInfo(roomHubProxy, sceneData);
        }

        public override void Initialize()
        {
            if (!realmData.Configured) return;

            commsRoomInfo.TryFetchNewInfo();

            PropagateToScene();
        }

        protected override void Update(float t)
        {
            if (initialized & !commsRoomInfo.TryFetchNewInfo() && !realmData.IsDirty)
                return;

            if (!realmData.Configured) // Realm is not configured for PX only // TODO review what should be propagated as PX has its own RealmData, here the global one is used
                return;

            PropagateToScene();
        }

        private void PropagateToScene()
        {
            initialized = true;

            ecsToCRDTWriter.PutMessage<PBRealmInfo, (IRealmData realmData, CommsRoomInfo commsRoomInfo)>(static (component, data) =>
            {
                component.BaseUrl = data.realmData.Ipfs.CatalystBaseUrl.Value;
                component.RealmName = data.realmData.RealmName;
                component.NetworkId = data.realmData.NetworkId;
                component.CommsAdapter = data.realmData.CommsAdapter;
                component.IsPreview = data.realmData.IsLocalSceneDevelopment;

                data.commsRoomInfo.WriteToComponent(component);
            }, SpecialEntitiesID.SCENE_ROOT_ENTITY, (realmData, commsRoomInfo));
        }

        public class CommsRoomInfo
        {
            private readonly ObjectProxy<IRoomHub> roomHubProxy;
            private readonly ISceneData sceneData;

            public string IslandSid { get; private set; }

            public bool IsConnectedSceneRoom { get; private set; }

            public CommsRoomInfo(ObjectProxy<IRoomHub> roomHubProxy, ISceneData sceneData)
            {
                this.roomHubProxy = roomHubProxy;
                this.sceneData = sceneData;
            }

            /// <summary>
            ///     Returns true if rooms info has changed
            /// </summary>
            /// <returns></returns>
            public bool TryFetchNewInfo()
            {
                IRoomHub roomHub = roomHubProxy.StrictObject;

                IGateKeeperSceneRoom sceneRoom = roomHub.SceneRoom();
                bool isConnectedToSceneRoom = sceneRoom.CurrentState() == IConnectiveRoom.State.Running && sceneRoom.IsSceneConnected(sceneData.SceneEntityDefinition.id);

                string room = roomHub.IslandRoom().Info.Sid ?? string.Empty;

                if (IsConnectedSceneRoom == isConnectedToSceneRoom && room == IslandSid)
                    return false;

                IsConnectedSceneRoom = isConnectedToSceneRoom;
                IslandSid = room;

                return true;
            }

            public void WriteToComponent(PBRealmInfo component)
            {
                component.Room = IslandSid;
                component.IsConnectedSceneRoom = IsConnectedSceneRoom;
            }
        }
    }
}
