using System;

namespace DCL.Multiplayer.Connections.Systems.RoomIndicator
{
    public struct DebugRoomIndicatorComponent
    {
        /// <summary>
        ///     If later needed can be moved to the non-debug scope
        ///     but for now I don't want to contaminate with the functionality needed only for debug
        /// </summary>
        [Flags]
        public enum RoomSource
        {
            NONE = 0,
            GATEKEEPER = 1,
            ISLAND = 1 << 1,
        }

        public readonly DebugRoomIndicatorView View;

        public RoomSource ConnectedTo;

        public DebugRoomIndicatorComponent(DebugRoomIndicatorView view)
        {
            View = view;
            ConnectedTo = RoomSource.NONE;
        }
    }
}
