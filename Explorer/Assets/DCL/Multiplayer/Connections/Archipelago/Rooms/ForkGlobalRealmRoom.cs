using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Archipelago.AdapterAddress.Current;
using DCL.Multiplayer.Connections.Archipelago.Rooms.Fixed;
using DCL.Multiplayer.Connections.Rooms.Connective;
using System;

namespace DCL.Multiplayer.Connections.Archipelago.Rooms
{
    public class ForkGlobalRealmRoom : ProxiedConnectiveRoomBase
    {
        private class Activatable : ActivatableConnectiveRoom, IArchipelagoIslandRoom
        {
            public Activatable(ForkGlobalRealmRoom origin, bool initialState = true) : base(origin, initialState) { }
        }

        private readonly ICurrentAdapterAddress currentAdapterAddress;
        private readonly Func<ArchipelagoIslandRoom> wssRoomFactory;
        private readonly Func<FixedConnectiveRoom> httpsRoomFactory;

        public ForkGlobalRealmRoom(ICurrentAdapterAddress currentAdapterAddress, Func<ArchipelagoIslandRoom> wssRoomFactory, Func<FixedConnectiveRoom> httpsRoomFactory)
        {
            this.currentAdapterAddress = currentAdapterAddress;
            this.wssRoomFactory = wssRoomFactory;
            this.httpsRoomFactory = httpsRoomFactory;
        }

        public IArchipelagoIslandRoom AsActivatable() =>
            new Activatable(this);

        public override UniTask<bool> StartAsync() =>
            Renew(ChooseRoom());

        private IConnectiveRoom ChooseRoom()
        {
            string adapterUrl = currentAdapterAddress.AdapterUrl();

            if (adapterUrl.Contains("wss://"))
                return wssRoomFactory();

            if (adapterUrl.Contains("https://"))
                return httpsRoomFactory();

            if (adapterUrl.Contains("offline:offline"))
                return new IConnectiveRoom.Null();

            throw new InvalidOperationException($"Cannot determine the protocol from the about url: {adapterUrl}");
        }
    }
}
