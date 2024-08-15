using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Rooms.Status;
using DCL.Multiplayer.Connections.Systems.Debug;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Multiplayer.Profiles.Tables;
using ECS.Abstract;
using System;

namespace DCL.Multiplayer.Connections.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ConnectionRoomsSystem))]
    public partial class DebugRoomsSystem : BaseUnityLoopSystem
    {
        private readonly IRoomDisplay roomDisplay;
        private readonly RoomsStatus roomsStatus;

        public DebugRoomsSystem(
            World world,
            RoomsStatus roomsStatus,
            IArchipelagoIslandRoom archipelagoIslandRoom,
            IGateKeeperSceneRoom gateKeeperSceneRoom,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            IRemotePoses remotePoses,
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

            var infoVisibilityBinding = new DebugWidgetVisibilityBinding(true);

            var infoWidget = debugBuilder
                            .AddWidget("Room: Info")
                            .SetVisibilityBinding(infoVisibilityBinding);

            var avatarsRoomDisplay = new AvatarsRoomDisplay(
                entityParticipantTable,
                infoWidget
            );

            var remotePosesRoomDisplay = new RemotePosesRoomDisplay(
                remotePoses,
                infoWidget
            );

            var infoRoomDisplay = new OnlyVisibleRoomDisplay(
                new SeveralRoomDisplay(
                    avatarsRoomDisplay,
                    remotePosesRoomDisplay
                ),
                infoVisibilityBinding
            );

            roomDisplay = new DebounceRoomDisplay(
                new SeveralRoomDisplay(
                    gateKeeperRoomDisplay,
                    archipelagoRoomDisplay,
                    infoRoomDisplay
                ),
                TimeSpan.FromSeconds(1)
            );

            this.roomsStatus = roomsStatus;
        }

        protected override void Update(float t)
        {
            roomDisplay.Update();
            roomsStatus.Update();
        }
    }
}
