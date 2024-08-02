using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Rooms.Connective;
using LiveKit.Rooms;

namespace DCL.Multiplayer.Connections.RoomHubs
{
    public class RoomHub : IRoomHub
    {
        private readonly IConnectiveRoom archipelagoIslandRoom;
        private readonly IConnectiveRoom gateKeeperSceneRoom;

        public RoomHub(IConnectiveRoom archipelagoIslandRoom, IConnectiveRoom gateKeeperSceneRoom)
        {
            this.archipelagoIslandRoom = archipelagoIslandRoom;
            this.gateKeeperSceneRoom = gateKeeperSceneRoom;
        }

        public IRoom IslandRoom() =>
            archipelagoIslandRoom.Room();

        public IRoom SceneRoom() =>
            gateKeeperSceneRoom.Room();

        public UniTask<bool> StartAsync()
        {
            archipelagoIslandRoom.StartAsync();
            gateKeeperSceneRoom.StartAsync();
            return UniTask.FromResult(true);
        }

        public UniTask StopIfNotAsync() =>
            UniTask.WhenAll(
                archipelagoIslandRoom.StopIfNotAsync(),
                gateKeeperSceneRoom.StopIfNotAsync()
            );
    }
}
