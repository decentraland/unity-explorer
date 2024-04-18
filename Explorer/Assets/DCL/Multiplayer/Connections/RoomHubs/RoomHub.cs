using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using LiveKit.Rooms;
using System.Threading;

namespace DCL.Multiplayer.Connections.RoomHubs
{
    public class RoomHub : IRoomHub
    {
        private readonly IRealmRoomsProvider archipelagoIslandRoom;
        private readonly IGateKeeperSceneRoomProvider gateKeeperSceneRoom;

        public RoomHub(IRealmRoomsProvider archipelagoIslandRoom, IGateKeeperSceneRoomProvider gateKeeperSceneRoom)
        {
            this.archipelagoIslandRoom = archipelagoIslandRoom;
            this.gateKeeperSceneRoom = gateKeeperSceneRoom;
        }

        public IRoom IslandRoom() =>
            archipelagoIslandRoom.Room();

        public IRoom SceneRoom() =>
            gateKeeperSceneRoom.Room();

        public UniTask StartAsync(CancellationToken ct) =>
            UniTask.WhenAll(
                archipelagoIslandRoom.StartAsync(ct),
                gateKeeperSceneRoom.StartAsync(ct));

        public UniTask StopAsync(CancellationToken ct) =>
            UniTask.WhenAll(
                archipelagoIslandRoom.StopAsync(ct),
                gateKeeperSceneRoom.StopAsync(ct)
            );
    }
}
