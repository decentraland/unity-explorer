using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
using LiveKit.Rooms;

namespace DCL.Multiplayer.Connections.RoomHubs
{
    public class RoomHub : IRoomHub
    {
        private readonly IConnectiveRoom archipelagoIslandRoom;
        private readonly IGateKeeperSceneRoom gateKeeperSceneRoom;
        private readonly IConnectiveRoom sharedPrivateConversationsRoom;

        public RoomHub(IConnectiveRoom archipelagoIslandRoom, IGateKeeperSceneRoom gateKeeperSceneRoom, IConnectiveRoom sharedPrivateConversationsRoom)
        {
            this.archipelagoIslandRoom = archipelagoIslandRoom;
            this.gateKeeperSceneRoom = gateKeeperSceneRoom;
            this.sharedPrivateConversationsRoom = sharedPrivateConversationsRoom;
        }

        public IRoom IslandRoom() =>
            archipelagoIslandRoom.Room();

        public IGateKeeperSceneRoom SceneRoom() =>
            gateKeeperSceneRoom;

        public IRoom SharedPrivateConversationsRoom() =>
            sharedPrivateConversationsRoom.Room();

        public async UniTask<bool> StartAsync()
        {
            var result = await UniTask.WhenAll(
                archipelagoIslandRoom.StartIfNotAsync(),
                gateKeeperSceneRoom.StartIfNotAsync(),
                sharedPrivateConversationsRoom.StartIfNotAsync()
            );

            return result is { Item1: true, Item2: true };
        }

        public UniTask StopAsync() =>
            UniTask.WhenAll(
                archipelagoIslandRoom.StopIfNotAsync(),
                gateKeeperSceneRoom.StopIfNotAsync(),
                sharedPrivateConversationsRoom.StartIfNotAsync()
            );
    }
}
