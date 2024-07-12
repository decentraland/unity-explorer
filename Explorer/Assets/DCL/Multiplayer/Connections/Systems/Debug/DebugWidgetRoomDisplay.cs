using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Multiplayer.Connections.Rooms.Connective;
using System;

namespace DCL.Multiplayer.Connections.Systems.Debug
{
    public class DebugWidgetRoomDisplay : IRoomDisplay
    {
        private readonly IConnectiveRoom room;
        private readonly ElementBinding<string> stateScene;
        private readonly ElementBinding<string> remoteParticipantsScene;
        private readonly ElementBinding<string> selfSid;
        private readonly ElementBinding<string> selfMetadata;
        private readonly ElementBinding<string> roomSid;
        private readonly ElementBinding<string> connectionQuality;
        private readonly ElementBinding<string> connectiveState;

        private readonly DebugWidgetVisibilityBinding visibilityBinding = new (false);

        public DebugWidgetRoomDisplay(
            string roomName,
            IConnectiveRoom connectiveRoom,
            IDebugContainerBuilder debugBuilder, Action<DebugWidgetBuilder>? postBuildAction = null
        ) : this(
            connectiveRoom, debugBuilder.AddWidget(roomName)!, postBuildAction
        ) { }

        public DebugWidgetRoomDisplay(IConnectiveRoom connectiveRoom, DebugWidgetBuilder widgetBuilder, Action<DebugWidgetBuilder>? postBuildAction = null)
        {
            room = connectiveRoom;
            selfSid = new ElementBinding<string>(string.Empty);
            roomSid = new ElementBinding<string>(string.Empty);
            connectionQuality = new ElementBinding<string>(string.Empty);
            stateScene = new ElementBinding<string>(string.Empty);
            remoteParticipantsScene = new ElementBinding<string>(string.Empty);
            selfMetadata = new ElementBinding<string>(string.Empty);
            connectiveState = new ElementBinding<string>(string.Empty);

            widgetBuilder
               .SetVisibilityBinding(visibilityBinding)!
               .AddCustomMarker("Room State", stateScene)!
               .AddCustomMarker("Connecting State", connectiveState)!
               .AddCustomMarker("Connection Quality", connectionQuality)!
               .AddCustomMarker("Remote Participants", remoteParticipantsScene)!
               .AddCustomMarker("Room Sid", roomSid)!
               .AddCustomMarker("Self Sid", selfSid)!
               .AddCustomMarker("Self Metadata", selfMetadata);

            postBuildAction?.Invoke(widgetBuilder);
        }

        public DebugWidgetRoomDisplay(
            IConnectiveRoom room,
            ElementBinding<string> stateScene,
            ElementBinding<string> remoteParticipantsScene,
            ElementBinding<string> selfSid,
            ElementBinding<string> connectionQuality,
            ElementBinding<string> roomSid,
            ElementBinding<string> selfMetadata,
            ElementBinding<string> connectiveState
        )
        {
            this.room = room;
            this.stateScene = stateScene;
            this.remoteParticipantsScene = remoteParticipantsScene;
            this.selfSid = selfSid;
            this.connectionQuality = connectionQuality;
            this.roomSid = roomSid;
            this.selfMetadata = selfMetadata;
            this.connectiveState = connectiveState;
        }

        public void Update()
        {
            if (visibilityBinding.IsExpanded == false)
                return;

            connectionQuality.SetAndUpdate(room.Room().Participants.LocalParticipant().ConnectionQuality.ToString());
            connectiveState.SetAndUpdate(room.Room().Info.ConnectionState.ToString());
            stateScene.SetAndUpdate(room.CurrentState().ToString());
            selfSid.SetAndUpdate(room.CurrentState() is IConnectiveRoom.State.Running ? room.Room().Participants.LocalParticipant().Sid : "Not connected");
            selfMetadata.SetAndUpdate(room.CurrentState() is IConnectiveRoom.State.Running ? room.Room().Participants.LocalParticipant().Metadata : "Not connected");
            roomSid.SetAndUpdate(room.CurrentState() is IConnectiveRoom.State.Running ? room.Room().Info.Sid : "Not connected");
            remoteParticipantsScene.SetAndUpdate(room.ParticipantCountInfo());
        }
    }
}
