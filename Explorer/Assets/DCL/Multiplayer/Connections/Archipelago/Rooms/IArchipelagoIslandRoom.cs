using DCL.Character;
using DCL.Multiplayer.Connections.Archipelago.AdapterAddress.Current;
using DCL.Multiplayer.Connections.Archipelago.Rooms.Fixed;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.Web3.Identities;
using DCL.WebRequests;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;

namespace DCL.Multiplayer.Connections.Archipelago.Rooms
{
    public interface IArchipelagoIslandRoom : IActivatableConnectiveRoom
    {
        public static IArchipelagoIslandRoom NewDefault(
            IWeb3IdentityCache identityCache,
            IMultiPool multiPool,
            IMemoryPool memoryPool,
            ICharacterObject characterObject,
            ICurrentAdapterAddress currentAdapterAddress,
            IWebRequestController webRequestController
        ) =>
            new ForkGlobalRealmRoom(
                currentAdapterAddress,
                () => new ArchipelagoIslandRoom(
                    characterObject,
                    identityCache,
                    multiPool,
                    memoryPool,
                    currentAdapterAddress
                ),
                () => new FixedConnectiveRoom(
                    webRequestController,
                    currentAdapterAddress
                )).AsActivatable();
    }
}
