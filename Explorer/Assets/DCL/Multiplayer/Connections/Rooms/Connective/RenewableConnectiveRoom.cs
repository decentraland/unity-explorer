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

        public void Start()
        {
            current = factory();
            current!.Start();
            room.Assign(current.Room(), out _);
        }

        public async UniTask StopAsync()
        {
            await (current?.StopAsync() ?? UniTask.CompletedTask);
            current = null;
            room.Assign(NullRoom.INSTANCE, out _);
        }

        public IConnectiveRoom.State CurrentState() =>
            current?.CurrentState() ?? IConnectiveRoom.State.Sleep;

        public IRoom Room() =>
            room;
    }
}
