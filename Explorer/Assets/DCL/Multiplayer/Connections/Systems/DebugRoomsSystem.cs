using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Systems.Debug;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Multiplayer.Profiles.Tables;
using ECS.Abstract;

namespace DCL.Multiplayer.Connections.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ConnectionRoomsSystem))]
    public partial class DebugRoomsSystem : BaseUnityLoopSystem
    {
        private readonly IRoomDisplay roomDisplay;

        public DebugRoomsSystem(
            World world,
            IRealmRoomsProvider archipelagoIslandRoom,
            IGateKeeperSceneRoomProvider gateKeeperSceneRoomProvider,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            IRemotePoses remotePoses,
            IDebugContainerBuilder debugBuilder
        ) : base(world)
        {
            var gateKeeperRoomDisplay = new DebugWidgetRoomDisplay(
                "Room: Scene",
                gateKeeperSceneRoomProvider,
                debugBuilder
            );

            var archipelagoRoomDisplay = new DebugWidgetRoomDisplay(
                "Room: Island",
                archipelagoIslandRoom,
                debugBuilder
            );

            var infoWidget = debugBuilder.AddWidget("Room: Info")!;

            var avatarsRoomDisplay = new AvatarsRoomDisplay(
                entityParticipantTable,
                infoWidget
            );

            var remotePosesRoomDisplay = new RemotePosesRoomDisplay(
                remotePoses,
                infoWidget
            );

            roomDisplay = new SeveralRoomDisplay(
                gateKeeperRoomDisplay,
                archipelagoRoomDisplay,
                avatarsRoomDisplay,
                remotePosesRoomDisplay
            );
        }

        protected override void Update(float t)
        {
            roomDisplay.Update();
        }
    }
}
