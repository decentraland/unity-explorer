using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character;
using DCL.Character.Components;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.FfiClients;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Systems;
using DCL.PlacesAPIService;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS.Abstract;
using LiveKit.Internal.FFIClients;
using System.Threading;
using UnityEngine;

namespace DCL.Multiplayer.Connections.Demo
{
    public class GateKeeperRoomPlayground : MonoBehaviour
    {
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

            IWeb3IdentityCache identityCache = new ProxyIdentityCache(
                new MemoryWeb3IdentityCache(),
                new PlayerPrefsIdentityProvider(
                    new PlayerPrefsIdentityProvider.DecentralandIdentityWithNethereumAccountJsonSerializer(),
                    ArchipelagoRoomPlayground.TestIdentityCache
                )
            );

            if (identityCache.Identity is null)
            {
                var identity = await new DappWeb3Authenticator.Default(identityCache)
                   .LoginAsync(CancellationToken.None);

                identityCache.Identity = identity;
            }

            identityCache.Identity = new LogWeb3Identity(identityCache.Identity);

            var character = new ICharacterObject.Fake(null!, null!, null!, Vector3.zero);
            var webRequests = new WebRequestController(identityCache);

            var gateKeeperRoom = new GateKeeperSceneRoom(
                webRequests,
                character,
                new PlacesAPIService.PlacesAPIService(new PlacesAPIClient(webRequests))
            );

            system = new ConnectionRoomsSystem(world, new IArchipelagoIslandRoom.Fake(), gateKeeperRoom);

            while (this)
            {
                system.Update(Time.deltaTime);
                await UniTask.Yield();
            }
        }
    }
}
