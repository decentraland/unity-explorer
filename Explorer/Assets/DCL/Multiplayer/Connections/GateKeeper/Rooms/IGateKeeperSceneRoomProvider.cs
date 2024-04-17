using DCL.Multiplayer.Connections.Archipelago.Rooms;

namespace DCL.Multiplayer.Connections.GateKeeper.Rooms
{
    public interface IGateKeeperSceneRoomProvider : IRoomProvider
    {
        class Fake : IRoomProvider.Fake, IGateKeeperSceneRoomProvider { }
    }
}
