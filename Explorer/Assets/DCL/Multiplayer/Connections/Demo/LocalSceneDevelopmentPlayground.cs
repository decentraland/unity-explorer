using Arch.Core;
using Best.HTTP.Shared;
using Cysharp.Threading.Tasks;
using DCL.Browser.DecentralandUrls;
using DCL.Character.Components;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.FfiClients;
using DCL.Multiplayer.Connections.GateKeeper.Meta;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.GateKeeper.Rooms.Options;
using DCL.Web3.Accounts.Factory;
using DCL.Web3.Identities;
using DCL.WebRequests;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.RequestsHub;
using ECS.SceneLifeCycle;
using Global.Dynamic.LaunchModes;
using LiveKit.Internal.FFIClients;
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

            var launchMode = ILaunchMode.LOCAL_SCENE_DEVELOPMENT;
            var urlsSource = new DecentralandUrlsSource(DecentralandEnvironment.Org, launchMode);

            IWeb3IdentityCache? identityCache = await ArchipelagoFakeIdentityCache.NewAsync(urlsSource, new Web3AccountFactory());
            var webRequests = new LogWebRequestController(new DefaultWebRequestController(new WebRequestsAnalyticsContainer(), identityCache, new RequestHub(urlsSource, HTTPManager.LocalCache, false, 0L, false)));

            var metaDataSource = new ConstSceneRoomMetaDataSource("random-name").WithLog();
            var options = new GateKeeperSceneRoomOptions(launchMode, urlsSource, metaDataSource, metaDataSource);

            new GateKeeperSceneRoom(
                    webRequests,
                    new ScenesCache(),
                    options
                ).StartAsync()
                 .Forget();
        }
    }
}
