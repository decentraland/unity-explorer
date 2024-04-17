using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
using System;

namespace DCL.Multiplayer.Connections.Systems.Debug
{
    public class DebugWidgetRoomDisplay : IRoomDisplay
    {
        private readonly IRoomProvider room;
        private readonly ElementBinding<string> stateScene;
        private readonly ElementBinding<string> remoteParticipantsScene;
        private readonly ElementBinding<string> selfSid;
        private readonly ElementBinding<string> selfMetadata;
        private readonly ElementBinding<string> roomSid;
        private readonly ElementBinding<string> connectionQuality;
        private readonly ElementBinding<string> connectiveState;

        public DebugWidgetRoomDisplay(
            string roomName,
            IRoomProvider roomProvider,
            IDebugContainerBuilder debugBuilder, Action<DebugWidgetBuilder>? postBuildAction = null
        ) : this(
            roomProvider, debugBuilder.AddWidget(roomName)!, postBuildAction
        ) { }

        public DebugWidgetRoomDisplay(IRoomProvider connectiveRoom, DebugWidgetBuilder widgetBuilder, Action<DebugWidgetBuilder>? postBuildAction = null)
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
               .SetVisibilityBinding(new DebugWidgetVisibilityBinding(true))!
               .AddCustomMarker("Room State", stateScene)!
               .AddCustomMarker("Connecting State", connectiveState)!
               .AddCustomMarker("Connection Quality", connectionQuality)!
               .AddCustomMarker("Remote Participants", remoteParticipantsScene)!
               .AddCustomMarker("Room Sid", roomSid)!
               .AddCustomMarker("Self Sid", selfSid)!
               .AddCustomMarker("Self Metadata", selfMetadata);

            postBuildAction?.Invoke(widgetBuilder);
        }

        public void Update()
        {
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
