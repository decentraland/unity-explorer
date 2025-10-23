using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.Cast;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.Multiplayer.Connections.Rooms.Status;
using DCL.Multiplayer.Connections.Systems.Debug;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Nametags;
using DCL.WebRequests;
using ECS.Abstract;
using RichTypes;
using System;
using System.Threading;

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
            IWebRequestController webRequestController,
            IDecentralandUrlsSource urls,
            CancellationToken lifetimeToken
        ) : base(world)
        {
            this.roomsStatus = roomsStatus;
            this.entityParticipantTable = entityParticipantTable;

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

            DCLCast dclCast = new DCLCast(webRequestController, urls);

            ElementBinding<string> tokenBinding = new (string.Empty);
            ElementBinding<string> statusBinding = new (string.Empty);

            debugBuilder
               .TryAddWidget(IDebugContainerBuilder.Categories.DCL_CAST)
              ?.AddControlWithLabel("Token", new DebugTextFieldDef(tokenBinding))
               .AddToggleField("Active", e => ToggleStreamAsync(e.newValue).Forget(), initialState: false)
               .AddCustomMarker("Status", statusBinding);

            return;

            async UniTaskVoid ToggleStreamAsync(bool enable)
            {
                if (enable)
                {
                    Result result = await dclCast.StartAsync(tokenBinding.Value, lifetimeToken);

                    if (result.Success)
                        statusBinding.Value = "Streaming";
                    else
                    {
                        string message = result.ErrorMessage ?? "Error on start cast";
                        statusBinding.Value = message;
                        ReportHub.LogError(ReportCategory.DCL_CAST, message);
                    }
                }
                else
                {
                    await dclCast.StopAsync(lifetimeToken);
                    statusBinding.Value = "Stopped";
                }
            }
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
