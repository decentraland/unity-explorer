using DCL.Multiplayer.Connections.Rooms.Connective;

namespace DCL.Multiplayer.Connections.Archipelago.Rooms
{
    internal interface IRealmRoomStrategy
    {
        IConnectiveRoom ConnectiveRoom { get; }
    }
}
