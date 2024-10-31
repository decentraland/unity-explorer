using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Multiplayer.Connections.Rooms.Connective;

namespace DCL.Multiplayer.Connections.Systems.Debug
{
    public class DebugWidgetRoomDisplay : IRoomDisplay
    {
        private const string ACTIVATE_BUTTON = "Activate";
        private const string DEACTIVATE_BUTTON = "Deactivate";

        private readonly IActivatableConnectiveRoom room;
        private readonly ElementBinding<string> stateScene;
        private readonly ElementBinding<string> remoteParticipantsScene;
        private readonly ElementBinding<string> selfSid;
        private readonly ElementBinding<string> selfMetadata;
        private readonly ElementBinding<string> roomSid;
        private readonly ElementBinding<string> connectionQuality;
        private readonly ElementBinding<string> connectiveState;
        private readonly ElementBinding<string> connectionLoopHealth;
        private readonly ElementBinding<string> activateButtonText;

        private readonly DebugWidgetVisibilityBinding visibilityBinding = new (true);

        private bool activated = true;

        protected static bool TryCreateWidget(IDebugContainerBuilder debugBuilder, string roomName, out DebugWidgetBuilder? widgetBuilder)
        {
            widgetBuilder = debugBuilder.TryAddWidget(roomName);
            return widgetBuilder != null;
        }

        public static IRoomDisplay Create(
            string roomName,
            IActivatableConnectiveRoom connectiveRoom,
            IDebugContainerBuilder debugBuilder
        )
        {
            if (!TryCreateWidget(debugBuilder, roomName, out DebugWidgetBuilder? widgetBuilder))
                return new IRoomDisplay.Null();

            return new DebugWidgetRoomDisplay(connectiveRoom, widgetBuilder!);
        }

        public DebugWidgetRoomDisplay(IActivatableConnectiveRoom connectiveRoom, DebugWidgetBuilder widgetBuilder)
        {
            room = connectiveRoom;
            selfSid = new ElementBinding<string>(string.Empty);
            roomSid = new ElementBinding<string>(string.Empty);
            connectionQuality = new ElementBinding<string>(string.Empty);
            stateScene = new ElementBinding<string>(string.Empty);
            remoteParticipantsScene = new ElementBinding<string>(string.Empty);
            selfMetadata = new ElementBinding<string>(string.Empty);
            connectiveState = new ElementBinding<string>(string.Empty);
            connectionLoopHealth = new ElementBinding<string>(string.Empty);
            activateButtonText = new ElementBinding<string>(ResolveActivateButtonText());

            widgetBuilder
               .SetVisibilityBinding(visibilityBinding)
               .AddCustomMarker("Room State", stateScene)
               .AddCustomMarker("Connecting State", connectiveState)
               .AddCustomMarker("Connection Loop", connectionLoopHealth)
               .AddCustomMarker("Connection Quality", connectionQuality)
               .AddCustomMarker("Remote Participants", remoteParticipantsScene)
               .AddCustomMarker("Room Sid", roomSid)
               .AddCustomMarker("Self Sid", selfSid)
               .AddCustomMarker("Self Metadata", selfMetadata)
               .AddSingleButton(activateButtonText, ActivateOrDeactivate);
        }

        public void Update()
        {
            if (visibilityBinding.IsConnectedAndExpanded == false)
                return;

            UpdateInternal();
        }

        protected virtual void UpdateInternal()
        {
            connectionQuality.SetAndUpdate(room.Room().Participants.LocalParticipant().ConnectionQuality.ToString());
            connectiveState.SetAndUpdate(room.Room().Info.ConnectionState.ToString());
            connectionLoopHealth.SetAndUpdate(room.CurrentConnectionLoopHealth.ToString());
            stateScene.SetAndUpdate(room.CurrentState().ToString());
            selfSid.SetAndUpdate(room.CurrentState() is IConnectiveRoom.State.Running ? room.Room().Participants.LocalParticipant().Sid : "Not connected");
            selfMetadata.SetAndUpdate(room.CurrentState() is IConnectiveRoom.State.Running ? room.Room().Participants.LocalParticipant().Metadata : "Not connected");
            roomSid.SetAndUpdate(room.CurrentState() is IConnectiveRoom.State.Running ? room.Room().Info.Sid : "Not connected");
            remoteParticipantsScene.SetAndUpdate(room.ParticipantCountInfo());
            UpdateActivateButtonText();
        }

        private void ActivateOrDeactivate()
        {
            if (activated)
                room.DeactivateAsync().Forget();
            else
                room.ActivateAsync().Forget();

            UpdateActivateButtonText();
        }

        private void UpdateActivateButtonText()
        {
            activateButtonText.SetAndUpdate(ResolveActivateButtonText());
        }

        private string ResolveActivateButtonText()
        {
            activated = room.Activated;
            return activated ? DEACTIVATE_BUTTON : ACTIVATE_BUTTON;
        }
    }
}
