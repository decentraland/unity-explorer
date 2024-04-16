using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Rooms;
using LiveKit.Rooms;

namespace DCL.Multiplayer.Connections.RoomHubs
{
    public interface IRoomHub
    {
        IRoom IslandRoom();

        IRoom SceneRoom();

        UniTask StartAsync();

        UniTask StopAsync();

        class Fake : IRoomHub
        {
            public IRoom IslandRoom() =>
                NullRoom.INSTANCE;

            public IRoom SceneRoom() =>
                NullRoom.INSTANCE;

            public UniTask StartAsync() =>
                UniTask.CompletedTask;

            public UniTask StopAsync() =>
                UniTask.CompletedTask;

            public void Reconnect()
            {
                //ignore
            }
        }
    }
}
