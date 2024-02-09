using DCL.Multiplayer.Connections.Rooms;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Rooms;

namespace DCL.Multiplayer.Connections.RoomHubs
{
    public class MutableRoomHub : IMutableRoomHub
    {
        private readonly IMultiPool multiPool;
        private readonly InteriorRoom islandPlayRoom = new ();
        private readonly InteriorRoom scenePlayRoom = new ();

        public MutableRoomHub(IMultiPool multiPool)
        {
            this.multiPool = multiPool;
        }

        public IRoom IslandRoom() =>
            islandPlayRoom;

        public IRoom SceneRoom() =>
            scenePlayRoom;

        public void AssignIslandRoom(IRoom playRoom)
        {
            islandPlayRoom.Assign(playRoom, out var previous);
            TryRelease(previous);
        }

        public void AssignSceneRoom(IRoom playRoom)
        {
            scenePlayRoom.Assign(playRoom, out var previous);
            TryRelease(previous);
        }

        private void TryRelease(IRoom? room)
        {
            if (room is Room r)
                multiPool.Release(r);
        }
    }
}
