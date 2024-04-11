using Cysharp.Threading.Tasks;
using LiveKit.Rooms;

namespace DCL.Multiplayer.Connections.Rooms.Connective
{
    public interface IConnectiveRoom
    {
        enum State
        {
            Sleep,
            Starting,
            Running,
        }

        void Start();

        UniTask StopAsync();

        State CurrentState();

        IRoom Room();

        class Fake : IConnectiveRoom
        {
            public void Start()
            {
                //ignore
            }

            public UniTask StopAsync() =>
                UniTask.CompletedTask;

            public State CurrentState() =>
                State.Sleep;

            public IRoom Room() =>
                NullRoom.INSTANCE;
        }
    }

    public static class GateKeeperSceneRoomExtensions
    {
        public static void StartIfNot(this IConnectiveRoom room)
        {
            if (room.CurrentState() is IConnectiveRoom.State.Sleep)
                room.Start();
        }

        public static async UniTask ReconnectAsync(this IConnectiveRoom room)
        {
            await room.StopAsync();
            room.Start();
        }

        public static string ParticipantCountInfo(this IConnectiveRoom room) =>
            room.CurrentState() is IConnectiveRoom.State.Running
                ? room.Room().Participants.RemoteParticipantSids().Count.ToString()
                : "Not connected";
    }
}
