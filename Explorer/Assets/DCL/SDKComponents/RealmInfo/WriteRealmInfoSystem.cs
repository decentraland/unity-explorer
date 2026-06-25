using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.Multiplayer.Connections.RoomHubs;
using ECS;
using ECS.Abstract;
using ECS.Groups;
using ECS.SceneLifeCycle;
using SceneRunner;
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

        internal WriteRealmInfoSystem(World world, IECSToCRDTWriter ecsToCRDTWriter, IRealmData realmData, IRoomHub roomHub, ISceneData sceneData, IScenesCache scenesCache)
            : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.realmData = realmData;

            commsRoomInfo = new CommsRoomInfo(roomHub, sceneData, scenesCache);
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
            private readonly IRoomHub roomHub;
            private readonly ISceneData sceneData;
            private readonly IScenesCache scenesCache;

            public string IslandSid { get; private set; }

            public bool IsConnectedSceneRoom { get; private set; }

            public CommsRoomInfo(IRoomHub roomHub, ISceneData sceneData, IScenesCache scenesCache)
            {
                this.roomHub = roomHub;
                this.sceneData = sceneData;
                this.scenesCache = scenesCache;
            }

            /// <summary>
            ///     Returns true if rooms info has changed
            /// </summary>
            /// <returns></returns>
            public bool TryFetchNewInfo()
            {
                // A Portable Experience scene connects through its own scene room, owned by its facade — not the
                // host's current scene room — so for a PX the connection state is read from the scene's facade and
                // the host scene-room check is omitted entirely (it is always false for a PX).
                bool isConnectedToSceneRoom = sceneData.IsPortableExperience()
                    ? IsPortableExperienceSceneRoomConnected()
                    : roomHub.SceneRoom().IsSceneConnected(sceneData.SceneEntityDefinition.id);

                string room = roomHub.IslandRoom().Info.Sid ?? string.Empty;

                if (IsConnectedSceneRoom == isConnectedToSceneRoom && room == IslandSid)
                    return false;

                IsConnectedSceneRoom = isConnectedToSceneRoom;
                IslandSid = room;

                return true;
            }

            private bool IsPortableExperienceSceneRoomConnected()
            {
                string sceneId = sceneData.SceneEntityDefinition.id;

                return !string.IsNullOrEmpty(sceneId)
                       && scenesCache.TryGetPortableExperienceBySceneUrn(sceneId, out ISceneFacade facade)
                       && facade is PortableExperienceSceneFacade { IsConnectedSceneRoom: true };
            }

            public void WriteToComponent(PBRealmInfo component)
            {
                component.Room = IslandSid;
                component.IsConnectedSceneRoom = IsConnectedSceneRoom;
            }
        }
    }
}
