using System;

namespace DCL.Multiplayer.Connections.Rooms
{
    /// <summary>
    ///     Identifies the room
    /// </summary>
    [Flags]
    public enum RoomSource : byte
    {
        None = 0,
        Gatekeeper = 1,
        Island = 1 << 1,
        Chat = 1 << 2,
        Pulse = 1 << 3,
    }
}
