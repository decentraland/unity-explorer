using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using LiveKit.Rooms;

namespace DCL.Multiplayer.Connections.Rooms.Connective
{
    /// <summary>
    ///     It's the base class for decorators with a branch logic
    /// </summary>
    public abstract class ProxiedConnectiveRoomBase : IConnectiveRoom
    {
        protected readonly InteriorRoom room = new ();
        internal readonly string logPrefix;

        protected IConnectiveRoom? current { get; private set; }

        protected ProxiedConnectiveRoomBase(IConnectiveRoom? current = null)
        {
            logPrefix = GetType().Name;

            this.current = current;

            if (current != null)
                room.Assign(current.Room(), out _);
        }

        protected async UniTask<bool> Renew(IConnectiveRoom newRoom)
        {
            if (current != null)
                await current.StopIfNotAsync();

            current = newRoom;
            room.Assign(newRoom.Room(), out _);
            return await newRoom.StartAsync();
        }

        /// <summary>
        ///     Stops and resets the current room
        /// </summary>
        public async UniTask ResetAsync()
        {
            await StopAsync();

            current = null;
            room.Assign(NullRoom.INSTANCE, out _);
        }

        public virtual UniTask<bool> StartAsync()
        {
            if (current == null)
            {
                ReportHub.Log(ReportCategory.LIVEKIT, $"{logPrefix} target is not assigned, calling {nameof(StartAsync)} has no effect");
                return UniTask.FromResult(true);
            }

            return current!.StartAsync();
        }

        public virtual UniTask StopAsync()
        {
            if (current == null)
            {
                ReportHub.Log(ReportCategory.LIVEKIT, $"{logPrefix} target is not assigned, calling {nameof(StopAsync)} has no effect");
                return UniTask.FromResult(true);
            }

            return current.StopAsync();
        }

        public virtual IConnectiveRoom.State CurrentState() =>
            current?.CurrentState() ?? IConnectiveRoom.State.Stopped;

        public virtual IConnectiveRoom.ConnectionLoopHealth CurrentConnectionLoopHealth => current?.CurrentConnectionLoopHealth ?? IConnectiveRoom.ConnectionLoopHealth.Stopped;

        public virtual IRoom Room() =>
            room;
    }
}
