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
                data.commsRoomInfo.WriteToComponent(component);

                // component.IsPreview // TODO: when E@ supports running in preview mode
            }, SpecialEntitiesID.SCENE_ROOT_ENTITY, (realmData, commsRoomInfo));
        }

        public class CommsRoomInfo
        {
            private readonly ObjectProxy<IRoomHub> roomHubProxy;
            private readonly ISceneData sceneData;

            public CommsRoomInfo(ObjectProxy<IRoomHub> roomHubProxy, ISceneData sceneData)
            {
                this.roomHubProxy = roomHubProxy;
                this.sceneData = sceneData;
            }

            public string IslandSid { get; private set; }

            public bool IsConnectedSceneRoom { get; private set; }

            /// <summary>
            ///     Returns true if rooms info has changed
            /// </summary>
            /// <returns></returns>
            public bool TryFetchNewInfo()
            {
                IRoomHub roomHub = roomHubProxy.StrictObject;

                IGateKeeperSceneRoom sceneRoom = roomHub.SceneRoom();
                bool isConnectedToSceneRoom = sceneRoom.CurrentState() == IConnectiveRoom.State.Running && sceneRoom.ConnectedScene == sceneData;

                string room = roomHub.IslandRoom().Info.Sid ?? string.Empty;

                if (IsConnectedSceneRoom == IsConnectedSceneRoom && room == IslandSid)
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
