using Cysharp.Threading.Tasks;
using LiveKit.Proto;
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

        UniTask<bool> StartAsync(string debugName = "");

        UniTask StopAsync();

        State CurrentState();

        AttemptToConnectState AttemptToConnectState { get; }

        ConnectionLoopHealth CurrentConnectionLoopHealth { get; }

        IRoom Room();

        class Null : IConnectiveRoom
        {
            public static readonly Null INSTANCE = new ();

            protected Null() { }

            public UniTask<bool> StartAsync(string debugName = "") =>
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
        private const string UNDEFINED = nameof(UNDEFINED);

        public static UniTask<bool> StartIfNotAsync(this IConnectiveRoom room, string debugName = "") =>
            room.CurrentState() is IConnectiveRoom.State.Stopped or IConnectiveRoom.State.Stopping
                ? room.StartAsync(debugName)
                : UniTask.FromResult(true);

        public static UniTask StopIfNotAsync(this IConnectiveRoom room) =>
            room.CurrentState() is IConnectiveRoom.State.Running
                ? room.StopAsync()
                : UniTask.CompletedTask;

        public static string ParticipantCountInfo(this IConnectiveRoom room) =>
            room.CurrentState() is IConnectiveRoom.State.Running
                ? room.Room().Participants.RemoteParticipantIdentities().Count.ToString()
                : "Not connected";

        public static string ToStringNonAlloc(this AttemptToConnectState state) =>
            state switch
            {
                AttemptToConnectState.None => "None",
                AttemptToConnectState.Success => "Success",
                AttemptToConnectState.Error => "Error",
                AttemptToConnectState.NoConnectionRequired => "NoConnectionRequired",
                _ => UNDEFINED,
            };

        public static string ToStringNonAlloc(this ConnectionQuality quality) =>
            quality switch
            {
                ConnectionQuality.QualityPoor => "QualityPoor",
                ConnectionQuality.QualityGood => "QualityGood",
                ConnectionQuality.QualityExcellent => "QualityExcellent",
                ConnectionQuality.QualityLost => "QualityLost",
                _ => UNDEFINED,
            };

        public static string ToStringNonAlloc(this ConnectionState state) =>
            state switch
            {
                ConnectionState.ConnDisconnected => "ConnDisconnected",
                ConnectionState.ConnConnected => "ConnConnected",
                ConnectionState.ConnReconnecting => "ConnReconnecting",
                _ => UNDEFINED,
            };

        public static string ToStringNonAlloc(this IConnectiveRoom.State state) =>
            state switch
            {
                IConnectiveRoom.State.Stopped => "Stopped",
                IConnectiveRoom.State.Starting => "Starting",
                IConnectiveRoom.State.Running => "Running",
                IConnectiveRoom.State.Stopping => "Stopping",
                _ => UNDEFINED,
            };

        public static string ToStringNonAlloc(this IConnectiveRoom.ConnectionLoopHealth health) =>
            health switch
            {
                IConnectiveRoom.ConnectionLoopHealth.Prewarming => "Prewarming",
                IConnectiveRoom.ConnectionLoopHealth.PrewarmFailed => "PrewarmFailed",
                IConnectiveRoom.ConnectionLoopHealth.Running => "PrewarmFailed",
                IConnectiveRoom.ConnectionLoopHealth.Stopped => "Stopped",
                IConnectiveRoom.ConnectionLoopHealth.CycleFailed => "CycleFailed",
                _ => UNDEFINED,
            };
    }
}
