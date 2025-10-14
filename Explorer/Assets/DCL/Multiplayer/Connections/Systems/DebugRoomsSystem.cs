using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
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
    [UpdateInGroup(typeof(PreRenderingSystemGroup))]
    [UpdateAfter(typeof(NametagPlacementSystem))]
    [LogCategory(ReportCategory.DEBUG)]
    public partial class DebugRoomsSystem : BaseUnityLoopSystem
    {
        private readonly IRoomDisplay roomDisplay;
        private readonly RoomsStatus roomsStatus;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly ElementBinding<bool> debugAvatarsRooms;
        private bool enabled;

        public DebugRoomsSystem(
            World world,
            RoomsStatus roomsStatus,
            IArchipelagoIslandRoom archipelagoIslandRoom,
            IGateKeeperSceneRoom gateKeeperSceneRoom,
            IActivatableConnectiveRoom chatRoom,
            IActivatableConnectiveRoom voiceChatRoom,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            IRemoteMetadata remoteMetadata,
            IDebugContainerBuilder debugBuilder,
            IObjectPool<DebugRoomIndicatorView> roomIndicatorPool) : base(world)
        {
            this.roomsStatus = roomsStatus;
            this.entityParticipantTable = entityParticipantTable;
            this.roomIndicatorPool = roomIndicatorPool;

            DebugWidgetBuilder? infoWidget = debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.ROOM_INFO);
            enabled = infoWidget != null;
            if (!enabled)
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

            IRoomDisplay chatRoomDisplay = DebugWidgetRoomDisplay.Create(
                IDebugContainerBuilder.Categories.ROOM_CHAT,
                chatRoom,
                debugBuilder
            );

            IRoomDisplay voiceChatRoomDisplay = DebugWidgetRoomDisplay.Create(
                IDebugContainerBuilder.Categories.ROOM_VOICE_CHAT,
                voiceChatRoom,
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
                new SeveralRoomDisplay(gateKeeperRoomDisplay, archipelagoRoomDisplay, infoRoomDisplay, chatRoomDisplay, voiceChatRoomDisplay),
                TimeSpan.FromSeconds(1)
            );

            this.roomsStatus = roomsStatus;
        }

        protected override void Update(float t)
        {
            if (!enabled) return;

            roomDisplay.Update();
            roomsStatus.Update();

            UpdateRoomIndicators();
        }

        partial void UpdateRoomIndicators();
    }
}
