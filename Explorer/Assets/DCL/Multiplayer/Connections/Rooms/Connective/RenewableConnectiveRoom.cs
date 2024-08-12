using Cysharp.Threading.Tasks;
using LiveKit.Rooms;
using System;

namespace DCL.Multiplayer.Connections.Rooms.Connective
{
    public class RenewableConnectiveRoom : IConnectiveRoom
    {
        private readonly Func<IConnectiveRoom> factory;
        private readonly InteriorRoom room = new ();
        private IConnectiveRoom? current;

        public RenewableConnectiveRoom(Func<IConnectiveRoom> factory)
        {
            this.factory = factory;
        }

        public async UniTask<bool> StartAsync()
        {
            current = factory();
            var task = current!.StartAsync();
            room.Assign(current.Room(), out _);
            return await task;
        }

        public async UniTask StopAsync()
        {
            await (current?.StopAsync() ?? UniTask.CompletedTask);
            current = null;
            room.Assign(NullRoom.INSTANCE, out _);
        }

        public IConnectiveRoom.State CurrentState() =>
            current?.CurrentState() ?? IConnectiveRoom.State.Stopped;

        public IRoom Room() =>
            room;
    }
}
