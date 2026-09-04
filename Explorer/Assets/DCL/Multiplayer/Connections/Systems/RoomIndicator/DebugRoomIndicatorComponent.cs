using DCL.Multiplayer.Connections.Rooms;

namespace DCL.Multiplayer.Connections.Systems.RoomIndicator
{
    public struct DebugRoomIndicatorComponent
    {
        /// <summary>Rooms whose data channel delivered an announcement for this avatar's wallet.</summary>
        public RoomSource Announced;

        /// <summary>LiveKit rooms whose participant roster lists this avatar's wallet.</summary>
        public RoomSource Present;
    }
}
