using Cysharp.Threading.Tasks;
using DCL.Character;
using DCL.Multiplayer.Connections.Archipelago.AdapterAddress.Current;
using DCL.Multiplayer.Connections.Archipelago.Rooms.Fixed;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.Web3.Identities;
using DCL.WebRequests;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Rooms;

namespace DCL.Multiplayer.Connections.Archipelago.Rooms
{
    public interface IArchipelagoIslandRoom : IConnectiveRoom
    {
        public static IArchipelagoIslandRoom NewDefault(
            IWeb3IdentityCache identityCache,
            IMultiPool multiPool,
            ICharacterObject characterObject,
            ICurrentAdapterAddress currentAdapterAddress,
            IWebRequestController webRequestController
        ) =>
            new RenewableArchipelagoIslandRoom(
                () => new ForkArchipelagoIslandRoom(
                    currentAdapterAddress,
                    () => new ArchipelagoIslandRoom(
                        characterObject,
                        identityCache,
                        multiPool,
                        currentAdapterAddress
                    ),
                    () => new FixedConnectiveRoom(
                        webRequestController,
                        currentAdapterAddress
                    )
                )
            );

        class Fake : IArchipelagoIslandRoom
        {
            public void Start()
            {
                //ignore
            }

            public UniTask StopAsync() =>
                UniTask.CompletedTask;

            //ignore
            public State CurrentState() =>
                State.Stopped;

            public IRoom Room() =>
                NullRoom.INSTANCE;
        }
    }
}
