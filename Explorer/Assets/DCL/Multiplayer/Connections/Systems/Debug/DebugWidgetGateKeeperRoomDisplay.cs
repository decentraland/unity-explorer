using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using UnityEngine;

namespace DCL.Multiplayer.Connections.Systems.Debug
{
    public class DebugWidgetGateKeeperRoomDisplay : DebugWidgetRoomDisplay
    {
        private readonly IGateKeeperSceneRoom connectiveRoom;
        private readonly ElementBinding<Vector2Int> associatedParcel;

        public DebugWidgetGateKeeperRoomDisplay(IGateKeeperSceneRoom connectiveRoom, DebugWidgetBuilder widgetBuilder)
            : base(connectiveRoom, widgetBuilder)
        {
            this.connectiveRoom = connectiveRoom;
            associatedParcel = new ElementBinding<Vector2Int>(Vector2Int.zero);
            widgetBuilder.AddControl(new DebugConstLabelDef("Associated Parcel"), new DebugVector2IntFieldDef(associatedParcel));
        }

        public static IRoomDisplay Create(
            string roomName,
            IGateKeeperSceneRoom connectiveRoom,
            IDebugContainerBuilder debugBuilder
        )
        {
            if (!TryCreateWidget(debugBuilder, roomName, out DebugWidgetBuilder? widgetBuilder))
                return new IRoomDisplay.Null();

            return new DebugWidgetGateKeeperRoomDisplay(connectiveRoom, widgetBuilder!);
        }

        protected override void UpdateInternal()
        {
            base.UpdateInternal();
            associatedParcel.SetAndUpdate(connectiveRoom.ConnectedScene?.SceneShortInfo.BaseParcel ?? Vector2Int.zero);
        }
    }
}
