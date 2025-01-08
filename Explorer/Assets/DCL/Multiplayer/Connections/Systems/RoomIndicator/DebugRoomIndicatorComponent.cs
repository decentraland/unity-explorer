using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Profiles.Entities;

namespace DCL.Multiplayer.Connections.Systems.RoomIndicator
{
    public struct DebugRoomIndicatorComponent
    {
        public readonly DebugRoomIndicatorView View;

        public RoomSource ConnectedTo;

        public DebugRoomIndicatorComponent(DebugRoomIndicatorView view)
        {
            View = view;
            ConnectedTo = RoomSource.NONE;
        }
    }
}
