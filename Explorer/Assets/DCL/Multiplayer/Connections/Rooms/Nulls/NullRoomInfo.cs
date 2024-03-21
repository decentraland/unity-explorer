using LiveKit.Rooms.Info;

namespace DCL.Multiplayer.Connections.Rooms.Nulls
{
    public class NullRoomInfo : IRoomInfo
    {
        public string Sid => "NullRoom: Sid null";
        public string Name => "NullRoom: Name null";
        public string Metadata => "NullRoom: Metadata null";

        public static readonly NullRoomInfo INSTANCE = new ();
    }
}
