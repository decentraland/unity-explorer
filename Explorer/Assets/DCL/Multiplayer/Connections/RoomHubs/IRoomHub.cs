using LiveKit.Rooms;

namespace DCL.Multiplayer.Connections.RoomHubs
{
    public interface IRoomHub
    {
        IRoom IslandRoom();

        IRoom SceneRoom();
    }
}
