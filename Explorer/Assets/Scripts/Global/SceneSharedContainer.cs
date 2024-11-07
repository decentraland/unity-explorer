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
using DCL.ResourcesUnloading;
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
    /// This class is never stored in a field and goes out of scope at the end of
    /// <see cref="Bootstrap.CreateGlobalWorld"/>. Consequently, it does not own any code and is not in
    /// fact a container.
    /// </summary>
    public class SceneSharedContainer
    {
        /// <summary>
        /// Is actually owned by <see cref="ECS.SceneLifeCycle.Systems.LoadSceneSystem"/>
        /// </summary>
        public ISceneFactory SceneFactory { get; private set; }

        /// <summary>
        /// Is actually collectively owned by some of the global plugins.
        /// <see cref="GlobalWorldFactory.Create"/> loops through all(?) global plugins and passes it to
        /// each.
        /// </summary>
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
            CacheCleaner cacheCleaner,
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
                    new SceneRuntimeFactory(staticContainer.WebRequestsContainer.WebRequestController,
                        realmData ?? new IRealmData.Fake(), new V8EngineFactory(v8ActiveEngines),
                        v8ActiveEngines, cacheCleaner, cacheJsSources),
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
