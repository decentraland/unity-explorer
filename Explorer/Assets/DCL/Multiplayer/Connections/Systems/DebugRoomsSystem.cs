using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Systems.Debug;
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
            IDebugContainerBuilder debugBuilder
        ) : base(world)
        {
            //TODO remove
            // var stateScene = new ElementBinding<string>(string.Empty);
            // var remoteParticipantsScene = new ElementBinding<string>(string.Empty);

            // debugBuilder.AddWidget("Rooms")!
            //             .SetVisibilityBinding(new DebugWidgetVisibilityBinding(true))!
            //             .AddCustomMarker("State", stateScene)!
            //             .AddCustomMarker("Remote Participants", remoteParticipantsScene);

            var gateKeeperRoomDisplay = new DebugPanelRoomDisplay(
                "Room: Scene",
                gateKeeperSceneRoom,
                debugBuilder
            );

            var archipelagoRoomDisplay = new DebugPanelRoomDisplay(
                "Room: Island",
                archipelagoIslandRoom,
                debugBuilder
            );

            roomDisplay = new SeveralRoomDisplay(gateKeeperRoomDisplay, archipelagoRoomDisplay);
        }

        protected override void Update(float t)
        {
            roomDisplay.Update();
        }
    }
}
