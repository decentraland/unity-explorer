using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Browser.DecentralandUrls;
using DCL.Character.Components;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.FfiClients;
using DCL.Multiplayer.Connections.GateKeeper.Meta;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.PlacesAPIService;
using DCL.Web3.Accounts.Factory;
using DCL.Web3.Identities;
using DCL.WebRequests;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.RequestsHub;
using ECS;
using ECS.SceneLifeCycle;
using LiveKit.Internal.FFIClients;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using UnityEngine;
using Utility;

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

            IWeb3IdentityCache? identityCache = await ArchipelagoFakeIdentityCache.NewAsync(urlsSource, new Web3AccountFactory());
            var character = new ExposedTransform();
            var webRequests = new LogWebRequestController(new WebRequestController(new WebRequestsAnalyticsContainer(), identityCache, new RequestHub(ITexturesFuse.NewDefault())));
            var places = new PlacesAPIService.PlacesAPIService(new PlacesAPIClient(webRequests, urlsSource));
            var realmData = new IRealmData.Fake();

            new GateKeeperSceneRoom(
                webRequests,
                new SceneRoomLogMetaDataSource(new SceneRoomMetaDataSource(realmData, character, places, false)),
                urlsSource,
                new ScenesCache()
            ).StartAsync();
        }
    }
}
