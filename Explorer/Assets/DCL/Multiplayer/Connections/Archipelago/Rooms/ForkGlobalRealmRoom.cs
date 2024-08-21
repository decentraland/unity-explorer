using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Archipelago.AdapterAddress.Current;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
using LiveKit.Rooms;
using System;

namespace DCL.Multiplayer.Connections.Archipelago.Rooms
{
    public class ForkGlobalRealmRoom : IArchipelagoIslandRoom
    {
        private readonly ICurrentAdapterAddress currentAdapterAddress;
        private readonly Func<IConnectiveRoom> wssRoomFactory;
        private readonly Func<IConnectiveRoom> httpsRoomFactory;

        private readonly InteriorRoom interiorRoom = new ();
        private IConnectiveRoom? current;

        public ForkGlobalRealmRoom(ICurrentAdapterAddress currentAdapterAddress, Func<IConnectiveRoom> wssRoomFactory, Func<IConnectiveRoom> httpsRoomFactory)
        {
            this.currentAdapterAddress = currentAdapterAddress;
            this.wssRoomFactory = wssRoomFactory;
            this.httpsRoomFactory = httpsRoomFactory;
        }

        public async UniTask<bool> StartAsync()
        {
            if (current != null && current.CurrentState() is not IConnectiveRoom.State.Stopped)
                throw new InvalidOperationException("First stop previous room before starting a new one");

            string adapterUrl = currentAdapterAddress.AdapterUrl();

            if (adapterUrl.Contains("wss://"))
                current = wssRoomFactory();
            else if (adapterUrl.Contains("https://"))
                current = httpsRoomFactory();
            else
                throw new InvalidOperationException($"Cannot determine the protocol from the about url: {adapterUrl}");

            var task = current!.StartAsync();
            interiorRoom.Assign(current.Room(), out _);
            return await task;
        }

        public UniTask StopAsync() =>
            current?.StopAsync() ?? throw new InvalidOperationException("Nothing to stop");

        public IConnectiveRoom.State CurrentState() =>
            current?.CurrentState() ?? IConnectiveRoom.State.Stopped;

        public IRoom Room() =>
            interiorRoom;
    }
}
