using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using LiveKit.Rooms;

namespace DCL.Multiplayer.Connections.Rooms.Connective
{
    /// <summary>
    ///     If room is deactivated it can't perform any operations,
    ///     its implementation will be replaced with <see cref="NullRoom" />
    /// </summary>
    public class ActivatableConnectiveRoom : IActivatableConnectiveRoom
    {
        private readonly IConnectiveRoom origin;
        private readonly string logPrefix;

        private readonly InteriorRoom proxy = new ();

        public bool Activated { get; private set; }
        public IConnectiveRoom.State OriginTargetState { get; private set; }

        public IConnectiveRoom.ConnectionLoopHealth CurrentConnectionLoopHealth => Activated ? origin.CurrentConnectionLoopHealth : IConnectiveRoom.ConnectionLoopHealth.Stopped;

        public ActivatableConnectiveRoom(IConnectiveRoom origin, bool initialState = true)
        {
            this.origin = origin;
            logPrefix = origin.GetType().Name;
            Activated = initialState;

            if (initialState)
                proxy.Assign(origin.Room(), out _);
        }

        /// <summary>
        ///     When activated the room will resume the most recent requested state (e.g. it will start according to the correct realm or parcel)
        /// </summary>
        /// <returns></returns>
        public UniTask ActivateAsync()
        {
            if (Activated)
            {
                ReportHub.Log(ReportCategory.LIVEKIT, $"{logPrefix} is already activated");
                return UniTask.CompletedTask;
            }

            Activated = true;

            proxy.Assign(origin.Room(), out _);

            return OriginTargetState is IConnectiveRoom.State.Starting or IConnectiveRoom.State.Running
                ? origin.StartAsync()
                : UniTask.CompletedTask;
        }

        /// <summary>
        ///     When deactivated the room will not activate on its own (e.g. when the realm or parcel has changed)
        /// </summary>
        public async UniTask DeactivateAsync()
        {
            if (!Activated)
            {
                ReportHub.Log(ReportCategory.LIVEKIT, $"{logPrefix} is already deactivated");
                return;
            }

            Activated = false;

            // Stop the room properly
            await origin.StopIfNotAsync();

            proxy.Assign(NullRoom.INSTANCE, out _);
        }

        public UniTask<bool> StartAsync()
        {
            OriginTargetState = IConnectiveRoom.State.Running;

            if (!Activated)
            {
                ReportHub.Log(ReportCategory.LIVEKIT, $"{logPrefix} is deactivated, the room will start when activated");

                // return "true" because it's a valid state, there was no error
                return UniTask.FromResult(true);
            }

            return origin.StartAsync();
        }

        public UniTask StopAsync()
        {
            OriginTargetState = IConnectiveRoom.State.Stopped;

            if (!Activated)
            {
                ReportHub.Log(ReportCategory.LIVEKIT, $"{logPrefix} was already stopped on deactivation");

                // return "true" because it's a valid state, there was no error
                return UniTask.CompletedTask;
            }

            return origin.StopAsync();
        }

        public IConnectiveRoom.State CurrentState() =>
            Activated ? origin.CurrentState() : IConnectiveRoom.State.Stopped;

        public IRoom Room() =>
            proxy;
    }
}
