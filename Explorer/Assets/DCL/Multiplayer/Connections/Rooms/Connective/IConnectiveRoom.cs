using Cysharp.Threading.Tasks;
using LiveKit.Rooms;
using System;

namespace DCL.Multiplayer.Connections.Rooms.Connective
{
    /// <summary>
    ///     Represent the core of the connection to a room
    /// </summary>
    public interface IConnectiveRoom : IDisposable
    {
        enum State
        {
            Stopped,
            Starting,
            Running,
            Stopping,
        }

        public enum ConnectionLoopHealth
        {
            Prewarming,

            PrewarmFailed,

            Running,

            /// <summary>
            ///     Gracefully stopped
            /// </summary>
            Stopped,

            CycleFailed,
        }

        UniTask<bool> StartAsync();

        UniTask StopAsync();

        State CurrentState();

        AttemptToConnectState AttemptToConnectState { get; }

        ConnectionLoopHealth CurrentConnectionLoopHealth { get; }

        IRoom Room();

        class Null : IConnectiveRoom
        {
            public static readonly Null INSTANCE = new ();

            protected Null() { }

            public UniTask<bool> StartAsync() =>
                UniTask.FromResult(true);

            public UniTask StopAsync() =>
                UniTask.CompletedTask;

            public State CurrentState() =>
                State.Stopped;

            public AttemptToConnectState AttemptToConnectState => AttemptToConnectState.None;

            public ConnectionLoopHealth CurrentConnectionLoopHealth => ConnectionLoopHealth.Stopped;

            public IRoom Room() =>
                NullRoom.INSTANCE;

            public void Dispose() { }
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
