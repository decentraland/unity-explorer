using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character;
using DCL.Character.Components;
using DCL.Multiplayer.Connections.Archipelago.AdapterAddress;
using DCL.Multiplayer.Connections.Archipelago.LiveConnections;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.Archipelago.SignFlow;
using DCL.Multiplayer.Connections.FfiClients;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Pools;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connections.Systems;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS.Abstract;
using LiveKit.Internal.FFIClients;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System.Net.WebSockets;
using UnityEngine;

namespace DCL.Multiplayer.Connections.Demo
{
    public class ArchipelagoRoomPlayground : MonoBehaviour
    {
        [SerializeField] private string aboutUrl = string.Empty;

        private BaseUnityLoopSystem system = null!;

        private void Start()
        {
            LaunchAsync().Forget();
        }

        private async UniTaskVoid LaunchAsync()
        {
            IFFIClient.Default.EnsureInitialize();

            var world = World.Create();
            world.Create(new CharacterTransform(new GameObject("Player").transform));

            var adapterAddresses = new LogAdapterAddresses(
                new RefinedAdapterAddresses(
                    new WebRequestsAdapterAddresses(
                        new LogWebRequestController(
                            IWebRequestController.DEFAULT
                        )
                    )
                ),
                Debug.Log
            );

            var memoryPool = new ArrayMemoryPool();

            var multiPool = new LogMultiPool(
                new ThreadSafeMultiPool(),
                Debug.Log
            );

            var signFlow = new LiveConnectionArchipelagoSignFlow(
                new LogArchipelagoLiveConnection(
                    new WebSocketArchipelagoLiveConnection(
                        new ClientWebSocket(),
                        memoryPool
                    ).WithAutoReconnect()
                ),
                memoryPool,
                multiPool
            );

            var messagePipeHub = new MessagePipesHub();

            //TODO message pipe with new approach, in PR with scene room
            var roomHub = new LogMutableRoomHub(
                new MessagePipedMutableRoomHub(
                    new MutableRoomHub(multiPool),
                    messagePipeHub,
                    multiPool,
                    memoryPool
                ),
                Debug.Log
            );

            IWeb3IdentityCache? identityCache = await ArchipelagoFakeIdentityCache.NewAsync();
            var character = new ICharacterObject.Fake(Vector3.zero);
            var archipelagoIslandRoom = new ArchipelagoIslandRoom(adapterAddresses, identityCache, signFlow, multiPool, character, aboutUrl);
            system = new ConnectionRoomsSystem(world, archipelagoIslandRoom, new IGateKeeperSceneRoom.Fake());

            while (this)
            {
                system.Update(Time.deltaTime);
                await UniTask.Yield();
            }
        }
    }
}
