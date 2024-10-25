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
            IRemoteMetadata remoteMetadata,
            IDebugContainerBuilder debugBuilder
        ) : base(world)
        {
            this.roomsStatus = roomsStatus;

            DebugWidgetBuilder? infoWidget = debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.ROOM_INFO);

            if (infoWidget == null)
            {
                roomDisplay = new IRoomDisplay.Null();
                return;
            }

            var infoVisibilityBinding = new DebugWidgetVisibilityBinding(true);

            infoWidget.SetVisibilityBinding(infoVisibilityBinding);

            IRoomDisplay gateKeeperRoomDisplay = DebugWidgetGateKeeperRoomDisplay.Create(
                IDebugContainerBuilder.Categories.ROOM_SCENE,
                gateKeeperSceneRoom,
                debugBuilder
            );

            IRoomDisplay archipelagoRoomDisplay = DebugWidgetRoomDisplay.Create(
                IDebugContainerBuilder.Categories.ROOM_ISLAND,
                archipelagoIslandRoom,
                debugBuilder
            );

            var avatarsRoomDisplay = new AvatarsRoomDisplay(
                entityParticipantTable,
                infoWidget
            );

            var remotePosesRoomDisplay = new RemotePosesRoomDisplay(
                remoteMetadata,
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
                new SeveralRoomDisplay(gateKeeperRoomDisplay, archipelagoRoomDisplay, infoRoomDisplay),
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
