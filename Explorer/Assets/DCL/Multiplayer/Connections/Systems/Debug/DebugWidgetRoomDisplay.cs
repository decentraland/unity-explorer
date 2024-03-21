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
        private readonly ElementBinding<string> roomId;
        private readonly ElementBinding<string> connectionQuality;

        public DebugWidgetRoomDisplay(
            string roomName,
            IConnectiveRoom connectiveRoom,
            IDebugContainerBuilder debugBuilder,
            Action<DebugWidgetBuilder>? postBuildAction = null
        ) : this(
            connectiveRoom, debugBuilder.AddWidget(roomName)!, postBuildAction
        ) { }

        public DebugWidgetRoomDisplay(IConnectiveRoom connectiveRoom, DebugWidgetBuilder widgetBuilder, Action<DebugWidgetBuilder>? postBuildAction = null)
        {
            room = connectiveRoom;
            roomId = new ElementBinding<string>(string.Empty);
            connectionQuality = new ElementBinding<string>(string.Empty);
            stateScene = new ElementBinding<string>(string.Empty);
            remoteParticipantsScene = new ElementBinding<string>(string.Empty);

            widgetBuilder
               .SetVisibilityBinding(new DebugWidgetVisibilityBinding(true))!
               .AddCustomMarker("Connecting State", stateScene)!
               .AddCustomMarker("Connection Quality", connectionQuality)!
               .AddCustomMarker("Remote Participants", remoteParticipantsScene)!
               .AddCustomMarker("Self Sid", roomId);

            postBuildAction?.Invoke(widgetBuilder);
        }

        public DebugWidgetRoomDisplay(
            IConnectiveRoom room,
            ElementBinding<string> stateScene,
            ElementBinding<string> remoteParticipantsScene,
            ElementBinding<string> roomId,
            ElementBinding<string> connectionQuality
        )
        {
            this.room = room;
            this.stateScene = stateScene;
            this.remoteParticipantsScene = remoteParticipantsScene;
            this.roomId = roomId;
            this.connectionQuality = connectionQuality;
        }

        public void Update()
        {
            connectionQuality.SetAndUpdate(room.Room().Participants.LocalParticipant().ConnectionQuality.ToString());
            stateScene.SetAndUpdate(room.CurrentState().ToString());
            roomId.SetAndUpdate(room.CurrentState() is IConnectiveRoom.State.Running ? room.Room().Participants.LocalParticipant().Sid : "Not connected");
            remoteParticipantsScene.SetAndUpdate(room.ParticipantCountInfo());
        }
    }
}
