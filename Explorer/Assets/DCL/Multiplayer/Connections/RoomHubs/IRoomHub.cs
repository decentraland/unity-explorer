using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Rooms;
using LiveKit.Rooms;
using System.Threading;

namespace DCL.Multiplayer.Connections.RoomHubs
{
    public interface IRoomHub
    {
        IRoom IslandRoom();

        IRoom SceneRoom();

        UniTask StartAsync(CancellationToken ct);

        UniTask StopAsync(CancellationToken ct);

        class Fake : IRoomHub
        {
            public IRoom IslandRoom() =>
                NullRoom.INSTANCE;

            public IRoom SceneRoom() =>
                NullRoom.INSTANCE;

            public UniTask StartAsync(CancellationToken ct) =>
                UniTask.CompletedTask;

            public UniTask StopAsync(CancellationToken ct) =>
                UniTask.CompletedTask;

            public void Reconnect()
            {
                //ignore
            }
        }
    }
}
