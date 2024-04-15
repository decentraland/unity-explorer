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
        private readonly Func<string, IConnectiveRoom> urlToWssRoomFactory;
        private readonly Func<string, IConnectiveRoom> urlToHttpsRoomFactory;

        private readonly InteriorRoom interiorRoom = new ();
        private IConnectiveRoom? current;

        public ForkArchipelagoIslandRoom(ICurrentAdapterAddress currentAdapterAddress, Func<string, IConnectiveRoom> urlToWssRoomFactory, Func<string, IConnectiveRoom> urlToHttpsRoomFactory)
        {
            this.currentAdapterAddress = currentAdapterAddress;
            this.urlToWssRoomFactory = urlToWssRoomFactory;
            this.urlToHttpsRoomFactory = urlToHttpsRoomFactory;
        }

        public void Start()
        {
            StartAsync().Forget();
        }

        private async UniTaskVoid StartAsync()
        {
            string aboutUrl = await currentAdapterAddress.AdapterUrlAsync(CancellationToken.None);

            if (aboutUrl.Contains("wss://"))
                current = urlToWssRoomFactory(aboutUrl);
            else if (aboutUrl.Contains("https://"))
                current = urlToHttpsRoomFactory(aboutUrl);
            else
                throw new InvalidOperationException($"Cannot determine the protocol from the about url: {aboutUrl}");

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
