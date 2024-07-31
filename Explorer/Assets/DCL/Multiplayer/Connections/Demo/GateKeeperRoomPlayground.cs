using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character;
using DCL.Character.Components;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.FfiClients;
using DCL.Multiplayer.Connections.GateKeeper.Meta;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.PlacesAPIService;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using LiveKit.Internal.FFIClients;
using UnityEngine;

namespace DCL.Multiplayer.Connections.Demo
{
    public class GateKeeperRoomPlayground : MonoBehaviour
    {
        private void Start()
        {
            LaunchAsync().Forget();
        }

        private async UniTaskVoid LaunchAsync()
        {
            IFFIClient.Default.EnsureInitialize();

            var world = World.Create();
            world.Create(new CharacterTransform(new GameObject("Player").transform));

            var urlsSource = new DecentralandUrlsSource(DecentralandEnvironment.Zone);

            IWeb3IdentityCache? identityCache = await ArchipelagoFakeIdentityCache.NewAsync();
            var character = new ICharacterObject.Fake(Vector3.zero);
            var webRequests = new LogWebRequestController(new WebRequestController(identityCache));
            var places = new PlacesAPIService.PlacesAPIService(new PlacesAPIClient(webRequests, urlsSource));
            var realmData = new IRealmData.Fake();

            new GateKeeperSceneRoom(
                webRequests,
                new LogMetaDataSource(new MetaDataSource(realmData, character, places)),
                urlsSource
            ).Start();
        }
    }
}
