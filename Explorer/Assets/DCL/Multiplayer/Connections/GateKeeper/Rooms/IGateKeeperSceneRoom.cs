using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
using LiveKit.Rooms;
using SceneRunner.Scene;

namespace DCL.Multiplayer.Connections.GateKeeper.Rooms
{
    public interface IGateKeeperSceneRoom : IConnectiveRoom
    {
        ISceneData? ConnectedScene { get; }

        class Fake : IGateKeeperSceneRoom
        {
            public ISceneData? ConnectedScene => null;

            public UniTask<bool> StartAsync() =>
                UniTask.FromResult(false);

            public UniTask StopAsync() =>
                UniTask.CompletedTask;

            public State CurrentState() =>
                State.Stopped;

            public IRoom Room() =>
                NullRoom.INSTANCE;
        }
    }
}
