using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
using LiveKit.Rooms;
using System.Threading;

namespace DCL.Multiplayer.Connections.Archipelago.Rooms
{
    /// <summary>
    /// This interface is not connected to <see cref="IConnectiveRoom"/>.
    /// It has some common fields but its contract can mutate independently
    /// </summary>
    public interface IRoomProvider
    {
        IRoom Room();

        IConnectiveRoom.State CurrentState();

        UniTask StartAsync(CancellationToken ct);

        UniTask StopAsync(CancellationToken ct);

        public class Fake : IRoomProvider
        {
            public IRoom Room() =>
                NullRoom.INSTANCE;

            public IConnectiveRoom.State CurrentState() =>
                IConnectiveRoom.State.Stopped;

            public UniTask StartAsync(CancellationToken ct) =>
                UniTask.CompletedTask;

            public UniTask StopAsync(CancellationToken ct) =>
                UniTask.CompletedTask;
        }
    }
}
