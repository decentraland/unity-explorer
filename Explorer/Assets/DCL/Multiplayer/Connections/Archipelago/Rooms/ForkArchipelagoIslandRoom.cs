using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Archipelago.AdapterAddress.Current;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
using LiveKit.Rooms;
using System;
using System.Threading;

namespace DCL.Multiplayer.Connections.Archipelago.Rooms
{
    public class ForkArchipelagoIslandRoom : IArchipelagoIslandRoom
    {
        private readonly ICurrentAdapterAddress currentAdapterAddress;
        private readonly Func<IConnectiveRoom> wssRoomFactory;
        private readonly Func<IConnectiveRoom> httpsRoomFactory;

        private readonly InteriorRoom interiorRoom = new ();
        private IConnectiveRoom? current;

        public ForkArchipelagoIslandRoom(ICurrentAdapterAddress currentAdapterAddress, Func<IConnectiveRoom> wssRoomFactory, Func<IConnectiveRoom> httpsRoomFactory)
        {
            this.currentAdapterAddress = currentAdapterAddress;
            this.wssRoomFactory = wssRoomFactory;
            this.httpsRoomFactory = httpsRoomFactory;
        }

        public void Start()
        {
            StartAsync().Forget();
        }

        private async UniTaskVoid StartAsync()
        {
            if (current != null && current.CurrentState() is not IConnectiveRoom.State.Stopped)
                throw new InvalidOperationException("First stop previous room before starting a new one");

            string adapterUrl = await currentAdapterAddress.AdapterUrlAsync(CancellationToken.None);

            if (adapterUrl.Contains("wss://"))
                current = wssRoomFactory();
            else if (adapterUrl.Contains("https://"))
                current = httpsRoomFactory();
            else
                throw new InvalidOperationException($"Cannot determine the protocol from the about url: {adapterUrl}");

            current!.Start();
            interiorRoom.Assign(current.Room(), out _);
        }

        public UniTask StopAsync() => current?.StopAsync() ?? throw new InvalidOperationException("Nothing to stop");

        public IConnectiveRoom.State CurrentState() =>
            current?.CurrentState() ?? IConnectiveRoom.State.Stopped;

        public IRoom Room() =>
            interiorRoom;
    }
}
