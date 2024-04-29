using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.Multiplayer.Connections.RoomHubs;
using ECS;
using ECS.Abstract;
using ECS.Groups;

namespace DCL.SDKComponents.RealmInfo
{
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    public partial class WriteRealmInfoSystem : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly IRealmData realmData;
        private readonly IRoomHub roomHub;

        internal WriteRealmInfoSystem(World world, IECSToCRDTWriter ecsToCRDTWriter, IRealmData realmData, IRoomHub roomHub) : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.realmData = realmData;
            this.roomHub = roomHub;
        }

        public override void Initialize()
        {
            PropagateToScene();
        }

        protected override void Update(float t)
        {
            // PropagateToScene();
        }

        private void PropagateToScene()
        {
            ecsToCRDTWriter.PutMessage<PBRealmInfo, (IRealmData realmData, IRoomHub roomHub)>(static (component, data) =>
            {
                component.BaseUrl = data.realmData.Ipfs.CatalystBaseUrl.Value;
                component.RealmName = data.realmData.RealmName;
                component.NetworkId = data.realmData.NetworkId;
                component.CommsAdapter = data.realmData.CommsAdapter;
                component.Room = data.roomHub.IslandRoom().Info.Sid; // TODO: Is this the correct prop to read??
                // component.IsPreview
            }, SpecialEntitiesID.SCENE_ROOT_ENTITY, (realmData, roomHub));
        }
    }
}
