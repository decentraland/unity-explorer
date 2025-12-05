using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Browser.DecentralandUrls;
using DCL.Character.Components;
using DCL.DebugUtilities.UIBindings;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.FfiClients;
using DCL.Multiplayer.Connections.GateKeeper.Meta;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.GateKeeper.Rooms.Options;
using DCL.Web3.Accounts.Factory;
using DCL.Web3.Identities;
using DCL.WebRequests;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.ChromeDevtool;
using DCL.WebRequests.RequestsHub;
using ECS;
using Global.Dynamic.LaunchModes;
using LiveKit.Internal.FFIClients;
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
#if UNITY_EDITOR
            IFFIClient.Default.EnsureInitialize();

            var world = World.Create();
            world.Create(new CharacterTransform(new GameObject("Player").transform));

            var launchMode = ILaunchMode.PLAY;
            var urlsSource = new DecentralandUrlsSource(DecentralandEnvironment.Zone, launchMode);

            IWeb3IdentityCache? identityCache = await ArchipelagoFakeIdentityCache.NewAsync(urlsSource, new Web3AccountFactory(), DecentralandEnvironment.Zone);
            var character = new ExposedTransform();
            var totalBudget = 15;

            var chromeDev = ChromeDevtoolProtocolClient.NewForTest();
            var webRequests = new LogWebRequestController(new WebRequestController(new WebRequestsAnalyticsContainer(null), identityCache, new RequestHub(urlsSource), chromeDev, new WebRequestBudget(totalBudget, new ElementBinding<ulong>((ulong)totalBudget))));
            var realmData = new IRealmData.Fake();

            var metaDataSource = new SceneRoomLogMetaDataSource(new SceneRoomMetaDataSource(realmData, character, world, false));
            var options = new GateKeeperSceneRoomOptions(launchMode, urlsSource, metaDataSource, metaDataSource);

            new GateKeeperSceneRoom(
                    webRequests,
                    options
                ).StartAsync()
                 .Forget();
#endif
        }
    }
}
