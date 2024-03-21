using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Multiplayer.Connections.Rooms.Connective;

namespace DCL.Multiplayer.Connections.Systems.Debug
{
    public class DebugPanelRoomDisplay : IRoomDisplay
    {
        private readonly IConnectiveRoom room;
        private readonly ElementBinding<string> stateScene;
        private readonly ElementBinding<string> remoteParticipantsScene;
        private readonly ElementBinding<string> roomId;
        private readonly ElementBinding<string> healthInfo;

        public DebugPanelRoomDisplay(string roomName, IConnectiveRoom connectiveRoom, IDebugContainerBuilder debugBuilder)
        {
            room = connectiveRoom;
            roomId = new ElementBinding<string>(string.Empty);
            healthInfo = new ElementBinding<string>(string.Empty);
            stateScene = new ElementBinding<string>(string.Empty);
            remoteParticipantsScene = new ElementBinding<string>(string.Empty);

            debugBuilder.AddWidget(roomName)!
                        .SetVisibilityBinding(new DebugWidgetVisibilityBinding(true))!
                        .AddCustomMarker("State", stateScene)!
                        .AddCustomMarker("Remote Participants", remoteParticipantsScene)!
                        .AddCustomMarker("Self Sid", roomId);
        }

        public DebugPanelRoomDisplay(
            IConnectiveRoom room,
            ElementBinding<string> stateScene,
            ElementBinding<string> remoteParticipantsScene,
            ElementBinding<string> roomId,
            ElementBinding<string> healthInfo
        )
        {
            this.room = room;
            this.stateScene = stateScene;
            this.remoteParticipantsScene = remoteParticipantsScene;
            this.roomId = roomId;
            this.healthInfo = healthInfo;
        }

        public void Update()
        {
            healthInfo.SetAndUpdate(HealthInfo(room));
            stateScene.SetAndUpdate(room.CurrentState().ToString());
            roomId.SetAndUpdate(room.Room().Participants.LocalParticipant().Sid);

            //roomId.SetAndUpdate(room.Room().);//TODO

            remoteParticipantsScene.SetAndUpdate(
                (room.CurrentState() is IConnectiveRoom.State.Running
                    ? room.Room().Participants.RemoteParticipantSids().Count
                    : 0
                ).ToString()
            );
        }

        private static string HealthInfo(IConnectiveRoom connectiveRoom) =>
            $"{connectiveRoom.CurrentState()}; participantsCount {(connectiveRoom.CurrentState() is IConnectiveRoom.State.Running ? connectiveRoom.Room().Participants.RemoteParticipantSids().Count : 0)}";
    }
}
