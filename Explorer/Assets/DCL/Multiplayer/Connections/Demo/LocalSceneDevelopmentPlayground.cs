using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Browser.DecentralandUrls;
using DCL.Character.Components;
using DCL.DemoWorlds;
using DCL.GlobalPartitioning;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.FfiClients;
using DCL.Multiplayer.Connections.GateKeeper.Meta;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Web3.Accounts.Factory;
using DCL.Web3.Identities;
using DCL.WebRequests;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.RequestsHub;
using ECS;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Cache;
using LiveKit.Internal.FFIClients;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using UnityEngine;
using Utility;

namespace DCL.Multiplayer.Connections.Demo
{
    public class LocalSceneDevelopmentPlayground : MonoBehaviour
    {
        private IDemoWorld demoWorld = null!;

        private void Start()
        {
            LaunchAsync().Forget();
        }

        private void Update()
        {
            demoWorld.Update();
        }

        private async UniTaskVoid LaunchAsync()
        {
            IFFIClient.Default.EnsureInitialize();

            var world = World.Create();
            world.Create(new CharacterTransform(new GameObject("Player").transform));

            var urlsSource = new DecentralandUrlsSource(DecentralandEnvironment.Org);

            IWeb3IdentityCache? identityCache = await ArchipelagoFakeIdentityCache.NewAsync(urlsSource, new Web3AccountFactory());
            var character = new ExposedTransform();
            var webRequests = new LogWebRequestController(new WebRequestController(new WebRequestsAnalyticsContainer(), identityCache, new RequestHub(ITexturesFuse.NewDefault(), false)));

            new GateKeeperSceneRoom(
                    webRequests,
                    new SceneRoomLogMetaDataSource(new SceneRoomMetaDataSource(new IRealmData.Fake(), character, world, false)),
                    urlsSource,
                    new ScenesCache()
                   // ,
                   //  DecentralandUrl.LocalGateKeeperSceneAdapter
                ).StartAsync()
                 .Forget();

            demoWorld = new DemoWorld(
                world,
                w => { },
                w => new LoadSceneDefinitionListSystem(w, webRequests, new NoCache<SceneDefinitions, GetSceneDefinitionList>(false, false)),
                w => new GlobalDeferredLoadingSystem(w, new NullPerformanceBudget(), new NullPerformanceBudget(), new SceneAssetLock())
            );
            demoWorld.SetUp();
        }
    }
}
