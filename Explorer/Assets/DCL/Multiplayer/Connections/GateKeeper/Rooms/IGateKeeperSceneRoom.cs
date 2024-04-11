using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
using LiveKit.Rooms;

namespace DCL.Multiplayer.Connections.GateKeeper.Rooms
{
    public interface IGateKeeperSceneRoom : IConnectiveRoom
    {
        class Fake : IGateKeeperSceneRoom
        {
            public void Start()
            {
                //ignore
            }

            public UniTask StopAsync() =>
                UniTask.CompletedTask;

            //ignore
            public State CurrentState() =>
                State.Stopped;

            public IRoom Room() =>
                NullRoom.INSTANCE;
        }
    }
}
