using Cysharp.Threading.Tasks;
using LiveKit.Rooms;

namespace DCL.Multiplayer.Connections.Rooms.Connective
{
    public interface IConnectiveRoom
    {
        enum State
        {
            Stopped,
            Starting,
            Running,
            Stopping
        }

        UniTask<bool> StartAsync();

        UniTask StopAsync();

        State CurrentState();

        IRoom Room();

        class Fake : IConnectiveRoom
        {
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

    public static class GateKeeperSceneRoomExtensions
    {
        public static UniTask<bool> StartIfNotAsync(this IConnectiveRoom room) =>
            room.CurrentState() is IConnectiveRoom.State.Stopped or IConnectiveRoom.State.Stopping
                ? room.StartAsync()
                : UniTask.FromResult(true);

        public static UniTask StopIfNotAsync(this IConnectiveRoom room) =>
            room.CurrentState() is IConnectiveRoom.State.Running
                ? room.StopAsync()
                : UniTask.CompletedTask;

        public static string ParticipantCountInfo(this IConnectiveRoom room) =>
            room.CurrentState() is IConnectiveRoom.State.Running
                ? room.Room().Participants.RemoteParticipantIdentities().Count.ToString()
                : "Not connected";
    }
}
