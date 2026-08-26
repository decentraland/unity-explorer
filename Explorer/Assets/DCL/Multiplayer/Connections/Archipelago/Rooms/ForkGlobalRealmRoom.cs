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
        private readonly Func<FixedConnectiveRoom> fixedRoomFactory;
        private readonly bool allowInsecureLocalHttp;

        public ForkGlobalRealmRoom(
            ICurrentAdapterAddress currentAdapterAddress,
            Func<ArchipelagoIslandRoom> wssRoomFactory,
            Func<FixedConnectiveRoom> fixedRoomFactory,
            bool allowInsecureLocalHttp = false)
        {
            this.currentAdapterAddress = currentAdapterAddress;
            this.wssRoomFactory = wssRoomFactory;
            this.fixedRoomFactory = fixedRoomFactory;
            this.allowInsecureLocalHttp = allowInsecureLocalHttp;
        }

        public IArchipelagoIslandRoom AsActivatable() =>
            new Activatable(this);

        public override UniTask<bool> StartAsync() =>
            RenewAsync(ChooseRoom());

        private IConnectiveRoom ChooseRoom()
        {
            string adapterUrl = currentAdapterAddress.AdapterUrl();

            if (adapterUrl.Contains("wss://", StringComparison.OrdinalIgnoreCase))
                return wssRoomFactory();

            if (adapterUrl.Contains("https://", StringComparison.OrdinalIgnoreCase))
                return fixedRoomFactory();

            if (IsLoopbackHttpAdapter(adapterUrl, allowInsecureLocalHttp))
                return fixedRoomFactory();

            if (adapterUrl.Contains("offline:offline", StringComparison.OrdinalIgnoreCase))
                return IConnectiveRoom.Null.INSTANCE;

            throw new InvalidOperationException($"Cannot determine the protocol from the about url: {adapterUrl}");
        }

        internal static bool IsLoopbackHttpAdapter(string adapterUrl, bool allowInsecureLocalHttp)
        {
            if (!allowInsecureLocalHttp)
                return false;

            int schemeIndex = adapterUrl.IndexOf("http://", StringComparison.OrdinalIgnoreCase);
            if (schemeIndex < 0)
                return false;

            string httpUrl = adapterUrl.Substring(schemeIndex);
            return Uri.TryCreate(httpUrl, UriKind.Absolute, out Uri? uri)
                   && uri.Scheme == Uri.UriSchemeHttp
                   && uri.IsLoopback;
        }
    }
}
