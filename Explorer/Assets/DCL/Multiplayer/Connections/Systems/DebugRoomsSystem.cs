using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connections.Rooms.Status;
using DCL.Multiplayer.Connections.Systems.Debug;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Nametags;
using ECS.Abstract;
using System;
using UnityEngine.Pool;

namespace DCL.Multiplayer.Connections.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(NametagPlacementSystem))]
    [LogCategory(ReportCategory.DEBUG)]
    public partial class DebugRoomsSystem : BaseUnityLoopSystem
    {
        private readonly IRoomDisplay roomDisplay;
        private readonly RoomsStatus roomsStatus;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly ElementBinding<bool> debugAvatarsRooms;

        public DebugRoomsSystem(
            World world,
            RoomsStatus roomsStatus,
            IArchipelagoIslandRoom archipelagoIslandRoom,
            IGateKeeperSceneRoom gateKeeperSceneRoom,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            IRemoteMetadata remoteMetadata,
            IDebugContainerBuilder debugBuilder,
            IObjectPool<DebugRoomIndicatorView> roomIndicatorPool) : base(world)
        {
            this.roomsStatus = roomsStatus;
            this.entityParticipantTable = entityParticipantTable;
            this.roomIndicatorPool = roomIndicatorPool;

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

            debugAvatarsRooms = avatarsRoomDisplay.debugAvatarsRooms;

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

            UpdateRoomIndicators();
        }

        partial void UpdateRoomIndicators();
    }
}
