using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.Multiplayer.Connections.Credentials.Hub;
using DCL.Multiplayer.Connections.Credentials.Hub.Archipelago.AdapterAddress;
using DCL.Multiplayer.Connections.FfiClients;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Pools;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connections.Systems;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using ECS.Abstract;
using LiveKit.Internal.FFIClients;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System.Threading;
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
            var world = World.Create();
            world.Create(new CharacterTransform(new GameObject("Player").transform));

            var memoryPool = new ArrayMemoryPool();

            var multiPool = new LogMultiPool(
                new ThreadSafeMultiPool(),
                Debug.Log
            );

            var adapterAddresses = new LogAdapterAddresses(
                new RefinedAdapterAddresses(
                    new WebRequestsAdapterAddresses()
                ),
                Debug.Log
            );

            IWeb3IdentityCache identityCache = new ProxyIdentityCache(
                new MemoryWeb3IdentityCache(),
                new PlayerPrefsIdentityProvider(
                    new PlayerPrefsIdentityProvider.DecentralandIdentityWithNethereumAccountJsonSerializer(),
                    "ArchipelagoTestIdentity"
                )
            );

            if (identityCache.Identity is null)
            {
                var identity = await new DappWeb3Authenticator.Default(identityCache)
                   .LoginAsync(CancellationToken.None);

                identityCache.Identity = identity;
            }
            identityCache.Identity = new LogWeb3Identity(identityCache.Identity);

            var credentialsHub = new LogCredentialsHub(
                new ArchipelagoCredentialsHub(
                    adapterAddresses,
                    memoryPool,
                    multiPool,
                    identityCache,
                    aboutUrl
                ),
                Debug.Log
            );

            var messagePipeHub = new MessagePipesHub();

            var roomHub = new LogMutableRoomHub(
                new MessagePipedMutableRoomHub(
                    new MutableRoomHub(multiPool),
                    messagePipeHub,
                    multiPool,
                    memoryPool
                ),
                Debug.Log
            );

            IFFIClient.Default.EnsureInitialize();
            system = new ConnectionRoomsSystem(world, roomHub, multiPool, credentialsHub);

            while (this)
            {
                system.Update(Time.deltaTime);
                await UniTask.Yield();
            }
        }
    }
}
