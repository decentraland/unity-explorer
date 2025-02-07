using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Browser.DecentralandUrls;
using DCL.Character.Components;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.FfiClients;
using DCL.Multiplayer.Connections.GateKeeper.Meta;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Web3.Accounts.Factory;
using DCL.Web3.Identities;
using DCL.WebRequests;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.RequestsHub;
using ECS.SceneLifeCycle;
using Global.Dynamic.LaunchModes;
using LiveKit.Internal.FFIClients;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using UnityEngine;

namespace DCL.Multiplayer.Connections.Demo
{
    public class LocalSceneDevelopmentPlayground : MonoBehaviour
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

            var urlsSource = new DecentralandUrlsSource(DecentralandEnvironment.Org, ILaunchMode.PLAY);

            IWeb3IdentityCache? identityCache = await ArchipelagoFakeIdentityCache.NewAsync(urlsSource, new Web3AccountFactory());
            var webRequests = new LogWebRequestController(new WebRequestController(new WebRequestsAnalyticsContainer(), identityCache, new RequestHub(ITexturesFuse.NewDefault(), false)));

            new GateKeeperSceneRoom(
                    webRequests,
                    new SceneRoomLogMetaDataSource(new ConstSceneRoomMetaDataSource("random-name")),
                    urlsSource,
                    new ScenesCache()
                ).StartAsync()
                 .Forget();
        }
    }
}
