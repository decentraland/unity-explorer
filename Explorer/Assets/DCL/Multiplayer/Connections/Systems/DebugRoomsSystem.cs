using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Systems.Debug;
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
            IArchipelagoIslandRoom archipelagoIslandRoom,
            IGateKeeperSceneRoom gateKeeperSceneRoom,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            IDebugContainerBuilder debugBuilder
        ) : base(world)
        {
            var gateKeeperRoomDisplay = new DebugWidgetRoomDisplay(
                "Room: Scene",
                gateKeeperSceneRoom,
                debugBuilder
            );

            var archipelagoRoomDisplay = new DebugWidgetRoomDisplay(
                "Room: Island",
                archipelagoIslandRoom,
                debugBuilder
            );

            var avatarsRoomDisplay = new AvatarsRoomDisplay(
                entityParticipantTable,
                debugBuilder
            );

            roomDisplay = new SeveralRoomDisplay(
                gateKeeperRoomDisplay,
                archipelagoRoomDisplay,
                avatarsRoomDisplay
            );
        }

        protected override void Update(float t)
        {
            roomDisplay.Update();
        }
    }
}
