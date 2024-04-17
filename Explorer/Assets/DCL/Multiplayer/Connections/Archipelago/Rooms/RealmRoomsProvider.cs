using Cysharp.Threading.Tasks;
using DCL.Character;
using DCL.Multiplayer.Connections.Archipelago.AdapterAddress.Current;
using DCL.Multiplayer.Connections.Archipelago.Rooms.Fixed;
using DCL.Multiplayer.Connections.Archipelago.SignFlow;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.Web3.Identities;
using DCL.WebRequests;
using LiveKit.Rooms;
using System;
using System.Threading;

namespace DCL.Multiplayer.Connections.Archipelago.Rooms
{
    /// <summary>
    ///     Constructs either a fixed connective room or a dynamic one based on the archipelago.
    ///     It has nothing to do with the room itself - it operates on the higher level
    /// </summary>
    public class RealmRoomsProvider : IRealmRoomsProvider
    {
        private readonly IWeb3IdentityCache identityCache;
        private readonly ICharacterObject characterObject;
        private readonly IWebRequestController webRequestController;
        private readonly IArchipelagoSignFlow archipelagoSignFlow;
        private readonly ICurrentAdapterAddress currentAdapterAddress;

        /// <summary>
        /// Maintain a single instance to be able to reconfigure and share with all consumers
        /// </summary>
        private readonly InteriorRoom sharedRoom = new ();

        private IRealmRoomStrategy? currentStrategy;

        public RealmRoomsProvider(
            IWeb3IdentityCache identityCache,
            ICharacterObject characterObject,
            IWebRequestController webRequestController,
            IArchipelagoSignFlow archipelagoSignFlow,
            ICurrentAdapterAddress currentAdapterAddress)
        {
            this.identityCache = identityCache;
            this.characterObject = characterObject;
            this.webRequestController = webRequestController;
            this.archipelagoSignFlow = archipelagoSignFlow;
            this.currentAdapterAddress = currentAdapterAddress;
        }

        public IRoom Room() =>
            sharedRoom;

        public IConnectiveRoom.State CurrentState() =>
            currentStrategy?.ConnectiveRoom.CurrentState() ?? IConnectiveRoom.State.Stopped;

        /// <summary>
        /// Should be called from RealmController
        /// </summary>
        /// <param name="ct"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public async UniTask StartAsync(CancellationToken ct)
        {
            if (currentStrategy != null && currentStrategy.ConnectiveRoom.CurrentState() is not IConnectiveRoom.State.Stopped)
                throw new InvalidOperationException("First stop previous room before starting a new one");

            string adapterUrl = await currentAdapterAddress.AdapterUrlAsync(ct);

            if (adapterUrl.Contains("wss://"))
                currentStrategy = CreateFixedConnectionRoomStrategy(adapterUrl);
            else if (adapterUrl.Contains("https://"))
                currentStrategy = CreateArchipelagoIslandRoomStrategy(adapterUrl);
            else
                throw new InvalidOperationException($"Cannot determine the protocol from the about url: {adapterUrl}");

            currentStrategy.ConnectiveRoom.Start();
        }

        public UniTask StopAsync(CancellationToken ct) =>
            currentStrategy?.ConnectiveRoom.StopAsync(ct) ?? UniTask.CompletedTask;

        private FixedConnectionRoomStrategy CreateFixedConnectionRoomStrategy(string adapterUrl) =>
            new (sharedRoom, webRequestController, adapterUrl);

        private ArchipelagoIslandRoomStrategy CreateArchipelagoIslandRoomStrategy(string adapterUrl) =>
            new (sharedRoom, identityCache, archipelagoSignFlow, characterObject, adapterUrl);
    }
}
