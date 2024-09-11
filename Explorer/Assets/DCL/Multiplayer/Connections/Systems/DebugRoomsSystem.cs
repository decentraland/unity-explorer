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
            this.roomsStatus = roomsStatus;

            bool gateKeeperRoomDisplayResult = DebugWidgetRoomDisplay.TryCreate(
                IDebugContainerBuilder.Categories.ROOM_SCENE,
                gateKeeperSceneRoom,
                debugBuilder,
                null,
                out var gateKeeperRoomDisplay
            );

            bool archipelagoRoomDisplayResult = DebugWidgetRoomDisplay.TryCreate(
                IDebugContainerBuilder.Categories.ROOM_ISLAND,
                archipelagoIslandRoom,
                debugBuilder,
                null,
                out var archipelagoRoomDisplay
            );

            var infoVisibilityBinding = new DebugWidgetVisibilityBinding(true);

            var infoWidget = debugBuilder
                            .TryAddWidget(IDebugContainerBuilder.Categories.ROOM_INFO)
                           ?.SetVisibilityBinding(infoVisibilityBinding);

            if (infoWidget == null)
            {
                roomDisplay = new IRoomDisplay.Null();
                return;
            }

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
                    gateKeeperRoomDisplay ?? new IRoomDisplay.Null() as IRoomDisplay,
                    archipelagoRoomDisplay ?? new IRoomDisplay.Null() as IRoomDisplay,
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
