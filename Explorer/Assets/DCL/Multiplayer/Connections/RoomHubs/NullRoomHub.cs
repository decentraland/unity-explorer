using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Rooms;
using LiveKit.Rooms;

namespace DCL.Multiplayer.Connections.RoomHubs
{
    public class NullRoomHub : IRoomHub
    {
        public static readonly NullRoomHub INSTANCE = new ();

        public IRoom IslandRoom() => NullRoom.INSTANCE;

        public IGateKeeperSceneRoom SceneRoom() =>
            new IGateKeeperSceneRoom.Fake();

        public UniTask<bool> StartAsync() => UniTask.FromResult(true);

        public UniTask StopAsync() =>
            UniTask.CompletedTask;
    }
}
