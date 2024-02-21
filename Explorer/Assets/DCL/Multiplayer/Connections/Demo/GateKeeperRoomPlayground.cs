using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character;
using DCL.Character.Components;
using DCL.Multiplayer.Connections.FfiClients;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.PlacesAPIService;
using DCL.Web3.Identities;
using DCL.WebRequests;
using LiveKit.Internal.FFIClients;
using LiveKit.Internal.FFIClients.Pools;
using UnityEngine;

namespace DCL.Multiplayer.Connections.Demo
{
    public class GateKeeperRoomPlayground : MonoBehaviour
    {
        [SerializeField] private string gateKeeperUrl = "https://comms-gatekeeper.decentraland.zone/get-scene-handler";

        private void Start()
        {
            LaunchAsync().Forget();
        }

        private async UniTaskVoid LaunchAsync()
        {
            IFFIClient.Default.EnsureInitialize();

            var world = World.Create();
            world.Create(new CharacterTransform(new GameObject("Player").transform));

            IWeb3IdentityCache? identityCache = await ArchipelagoFakeIdentityCache.NewAsync();
            var character = new ICharacterObject.Fake(Vector3.zero);
            var webRequests = new LogWebRequestController(new WebRequestController(identityCache));
            var places = new PlacesAPIService.PlacesAPIService(new PlacesAPIClient(webRequests));
            var multiPool = new ThreadSafeMultiPool();

            new GateKeeperSceneRoom(
                webRequests,
                character,
                places,
                multiPool,
                gateKeeperUrl
            ).Start();
        }
    }
}
