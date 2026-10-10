using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Archipelago.AdapterAddress.Current;
using DCL.Multiplayer.Connections.Archipelago.Rooms.Fixed;
using DCL.Multiplayer.Connections.Rooms.Connective;
using System;
using Utility.Networking;

namespace DCL.Multiplayer.Connections.Archipelago.Rooms
{
    public class ForkGlobalRealmRoom : ProxiedConnectiveRoomBase
    {
        /// <summary>The room an adapter url asks for.</summary>
        internal enum AdapterProtocol
        {
            Archipelago,
            Fixed,
            Offline,
        }

        private readonly ICurrentAdapterAddress currentAdapterAddress;
        private readonly Func<ArchipelagoIslandRoom> wssRoomFactory;
        private readonly Func<FixedConnectiveRoom> fixedRoomFactory;
        private readonly bool allowInsecureLocalHttp;

        public ForkGlobalRealmRoom(
            ICurrentAdapterAddress currentAdapterAddress,
            Func<ArchipelagoIslandRoom> wssRoomFactory,
            Func<FixedConnectiveRoom> fixedRoomFactory,
            bool allowInsecureLocalHttp)
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

        /// <summary>
        ///     Which room serves <paramref name="adapterUrl" />, as refined by
        ///     <see cref="AdapterAddress.RefinedAdapterAddresses" />. Throws when the address names no protocol
        ///     this client speaks, so an unreadable one fails the connection instead of silently going offline.
        /// </summary>
        internal static AdapterProtocol ProtocolFor(string adapterUrl, bool allowInsecureLocalHttp)
        {
            if (adapterUrl.Contains("wss://", StringComparison.OrdinalIgnoreCase))
                return AdapterProtocol.Archipelago;

            if (adapterUrl.Contains("https://", StringComparison.OrdinalIgnoreCase))
                return AdapterProtocol.Fixed;

            // The local fixture exposes Archipelago through cleartext WebSockets. It is still the Archipelago
            // protocol, so keep it on the island-room path while requiring the same explicit local opt-in used
            // for the fixture's cleartext HTTP endpoints.
            if (allowInsecureLocalHttp && LoopbackUrls.IsLoopbackWsUrl(adapterUrl))
                return AdapterProtocol.Archipelago;

            // A cleartext adapter is only ever a local fixture, and only where the operator asked for one on
            // the command line: over http the handshake this room signs is readable and rewritable in transit.
            if (allowInsecureLocalHttp && LoopbackUrls.IsLoopbackHttpUrl(adapterUrl))
                return AdapterProtocol.Fixed;

            if (adapterUrl.Contains("offline:offline", StringComparison.OrdinalIgnoreCase))
                return AdapterProtocol.Offline;

            throw new InvalidOperationException($"Cannot determine the protocol from the about url: {adapterUrl}");
        }

        private IConnectiveRoom ChooseRoom() =>
            ProtocolFor(currentAdapterAddress.AdapterUrl(), allowInsecureLocalHttp) switch
            {
                AdapterProtocol.Archipelago => wssRoomFactory(),
                AdapterProtocol.Fixed => fixedRoomFactory(),
                _ => IConnectiveRoom.Null.INSTANCE,
            };

        private class Activatable : ActivatableConnectiveRoom, IArchipelagoIslandRoom
        {
            public Activatable(ForkGlobalRealmRoom origin, bool initialState = true) : base(origin, initialState) { }
        }
    }
}
