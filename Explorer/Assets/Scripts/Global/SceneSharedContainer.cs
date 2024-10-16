using CRDT.Serializer;
using CrdtEcsBridge.JsModulesImplementation.Communications;
using CrdtEcsBridge.PoolsProviders;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Poses;
using DCL.PluginSystem.World.Dependencies;
using MVC;
using DCL.Profiles;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using Global.Dynamic;
using SceneRunner;
using SceneRunner.ECSWorld;
using SceneRuntime;
using SceneRuntime.Factory;

namespace Global
{
    /// <summary>
    ///     Holds dependencies shared between all scene instances. <br />
    ///     Consider breaking down this class as much as needed if the number of dependencies grows
    /// </summary>
    public class SceneSharedContainer
    {
        public ISceneFactory SceneFactory { get; private set; }
        public V8ActiveEngines V8ActiveEngines { get; private set; }

        public static SceneSharedContainer Create(in StaticContainer staticContainer,
            IDecentralandUrlsSource decentralandUrlsSource,
            IWeb3IdentityCache web3IdentityCache,
            IWebRequestController webRequestController,
            IRealmData realmData,
            IProfileRepository profileRepository,
            IRoomHub roomHub,
            IMVCManager mvcManager,
            IMessagePipesHub messagePipesHub,
            IRemoteMetadata remoteMetadata,
            bool cacheJsSources = true)
        {
            ECSWorldSingletonSharedDependencies sharedDependencies = staticContainer.SingletonSharedDependencies;
            ExposedGlobalDataContainer exposedGlobalDataContainer = staticContainer.ExposedGlobalDataContainer;

            var ecsWorldFactory = new ECSWorldFactory(sharedDependencies,
                staticContainer.PartitionSettings,
                exposedGlobalDataContainer.CameraSamplingData,
                staticContainer.ECSWorldPlugins);

            var v8ActiveEngines = new V8ActiveEngines();

            return new SceneSharedContainer
            {
                V8ActiveEngines = v8ActiveEngines,
                SceneFactory = new SceneFactory(
                    ecsWorldFactory,
                    new SceneRuntimeFactory(staticContainer.WebRequestsContainer.WebRequestController, realmData ?? new IRealmData.Fake(), new V8EngineFactory(v8ActiveEngines), v8ActiveEngines, cacheJsSources),
                    new SharedPoolsProvider(),
                    new CRDTSerializer(),
                    staticContainer.ComponentsContainer.SDKComponentsRegistry,
                    sharedDependencies.EntityFactory,
                    staticContainer.EntityCollidersGlobalCache,
                    staticContainer.EthereumApi,
                    mvcManager,
                    profileRepository,
                    web3IdentityCache,
                    decentralandUrlsSource,
                    webRequestController,
                    roomHub,
                    realmData,
                    staticContainer.PortableExperiencesController,
                    new SceneCommunicationPipe(messagePipesHub, roomHub.SceneRoom()), remoteMetadata),
            };
        }
    }
}
