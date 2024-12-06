using System;

namespace DCL.Multiplayer.Connections.Rooms
{
    /// <summary>
    ///     Identifies the room
    /// </summary>
    [Flags]
    public enum RoomSource : byte
    {
        NONE = 0,
        GATEKEEPER = 1,
        ISLAND = 1 << 1,
    }
}
