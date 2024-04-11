using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
using LiveKit.Rooms;
using System;

namespace DCL.Multiplayer.Connections.Archipelago.Rooms
{
    public class RenewableArchipelagoIslandRoom : IArchipelagoIslandRoom
    {
        private readonly Func<IArchipelagoIslandRoom> factory;
        private readonly InteriorRoom room = new ();
        private IArchipelagoIslandRoom? current;

        public RenewableArchipelagoIslandRoom(Func<IArchipelagoIslandRoom> factory)
        {
            this.factory = factory;
        }

        public void Start()
        {
            current = factory()!;
            room.Assign(current.Room(), out _);
            current.Start();
        }

        public async UniTask StopAsync()
        {
            await (current?.StopAsync() ?? UniTask.CompletedTask);
            room.Assign(NullRoom.INSTANCE, out _);
        }

        public IConnectiveRoom.State CurrentState() =>
            current?.CurrentState() ?? IConnectiveRoom.State.Stopped;

        public IRoom Room() =>
            room;
    }
}
