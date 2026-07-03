using LiveKit.Proto;
using LiveKit.Rooms.Info;
using DCL.LiveKit.Public;

namespace DCL.Multiplayer.Connections.Rooms.Nulls
{
    public class NullRoomInfo : IRoomInfo
    {
        public const string METADATA_SENTINEL = "NullRoom: Metadata null";

        public LKConnectionState ConnectionState => LKConnectionState.ConnDisconnected;
        public string Sid => "NullRoom: Sid null";
        public string Name => "NullRoom: Name null";
        public string Metadata => METADATA_SENTINEL;

        public static readonly NullRoomInfo INSTANCE = new ();
    }
}
