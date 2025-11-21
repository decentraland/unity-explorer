using CRDT.Serializer;
using CrdtEcsBridge.JsModulesImplementation.Communications;
using CrdtEcsBridge.PoolsProviders;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Poses;
using DCL.PluginSystem.World.Dependencies;
using DCL.Clipboard;
using MVC;
using DCL.Profiles;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using Global.AppArgs;
using Global.Dynamic;
using SceneRunner;
using SceneRunner.ECSWorld;
using SceneRuntime;
using SceneRuntime.Factory;
using SceneRuntime.Factory.WebSceneSource;

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

        public static SceneSharedContainer Create(
            in StaticContainer staticContainer,
            IDecentralandUrlsSource decentralandUrlsSource,
            IWeb3IdentityCache web3IdentityCache,
            IWebRequestController webRequestController,
            IRealmData realmData,
            IProfileRepository profileRepository,
            IRoomHub roomHub,
            IMVCManager mvcManager,
            IMessagePipesHub messagePipesHub,
            IRemoteMetadata remoteMetadata,
            IWebJsSources webJsSources,
            DecentralandEnvironment dclEnvironment,
            IAppArgs appArgs,
            ISystemClipboard systemClipboard
        )
        {
            ECSWorldSingletonSharedDependencies sharedDependencies = staticContainer.SingletonSharedDependencies;
            ExposedGlobalDataContainer exposedGlobalDataContainer = staticContainer.ExposedGlobalDataContainer;

            var ecsWorldFactory = new ECSWorldFactory(sharedDependencies,
                staticContainer.PartitionSettings,
                exposedGlobalDataContainer.CameraSamplingData,
                staticContainer.ECSWorldPlugins);

            return new SceneSharedContainer
            {
                SceneFactory = new SceneFactory(
                    ecsWorldFactory: ecsWorldFactory,
                    sceneRuntimeFactory: new SceneRuntimeFactory(
                        realmData ?? new IRealmData.Fake(),
                        new V8EngineFactory(),
                        webJsSources
                    ),
                    sharedPoolsProvider: new SharedPoolsProvider(),
                    crdtSerializer: new CRDTSerializer(),
                    sdkComponentsRegistry: staticContainer.ComponentsContainer.SDKComponentsRegistry,
                    entityFactory: sharedDependencies.EntityFactory,
                    entityCollidersGlobalCache: staticContainer.EntityCollidersGlobalCache,
                    ethereumApi: staticContainer.EthereumApi,
                    mvcManager: mvcManager,
                    profileRepository: profileRepository,
                    identityCache: web3IdentityCache,
                    decentralandUrlsSource: decentralandUrlsSource,
                    webRequestController: webRequestController,
                    roomHub: roomHub,
                    realmData: realmData,
                    portableExperiencesController: staticContainer.PortableExperiencesController,
                    skyboxSettings: staticContainer.StaticSettings.SkyboxSettings,
                    messagePipesHub: new SceneCommunicationPipe(
                        messagePipesHub,
                        roomHub.SceneRoom()
                    ),
                    remoteMetadata: remoteMetadata,
                    systemClipboard: systemClipboard,
                    dclEnvironment: dclEnvironment,
                    appArgs: appArgs
                ),
            };
        }
    }
}
