using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Rooms;
using LiveKit.Rooms;

namespace DCL.Multiplayer.Connections.RoomHubs
{
    public interface IRoomHub
    {
        IRoom IslandRoom();

        IRoom SceneRoom();

        UniTask<bool> StartAsync();

        UniTask StopIfNotAsync();

        class Fake : IRoomHub
        {
            public IRoom IslandRoom() =>
                NullRoom.INSTANCE;

            public IRoom SceneRoom() =>
                NullRoom.INSTANCE;

            public UniTask<bool> StartAsync() =>
                UniTask.FromResult(true);

            public UniTask StopIfNotAsync() =>
                UniTask.CompletedTask;

            public void Reconnect()
            {
                //ignore
            }
        }
    }
}
