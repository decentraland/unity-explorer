using LiveKit.Rooms;

namespace DCL.Multiplayer.Connections.RoomHubs
{
    public interface IMutableRoomHub : IRoomHub
    {
        void AssignIslandRoom(IRoom playRoom);

        void AssignSceneRoom(IRoom playRoom);
    }
}
